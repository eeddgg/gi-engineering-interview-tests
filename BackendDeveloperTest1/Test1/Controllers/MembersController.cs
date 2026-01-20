using Microsoft.AspNetCore.Mvc;
using Dapper;
using Test1.Contracts;
using Test1.Models;
using System.Data;

namespace Test1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MembersController : ControllerBase
    {
        private readonly ISessionFactory _sessionFactory;
        public MembersController(ISessionFactory sessionFactory)
        {
            _sessionFactory = sessionFactory;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MemberDto>>> List(CancellationToken cancellationToken)
        {
            var dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                .ConfigureAwait(false);


            // I recommend not using reserved keywords as column identifiers, as it leads to errors during development
            const string sql = @"SELECT 
                Guid, AccountUid, LocationUid, `Primary`, 
                JoinedDateUtc, CancelDateUtc, FirstName, LastName, 
                Address, City, Locale, PostalCode, Cancelled
            FROM member m";


            // get all members
            var rows = await dbContext.Session.QueryAsync<MemberDto>(sql, dbContext.Transaction).ConfigureAwait(false);

            dbContext.Commit();

            dbContext.Dispose();

            // Map account to each member for completeness of information
            foreach (MemberDto row in rows)
            {
                dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                .ConfigureAwait(false);

                var tempAccount = await AccountsController.GetAccountByUID(row.AccountUid, dbContext).ConfigureAwait(false);

                row.Account = tempAccount;

                await dbContext.DisposeAsync();

            }

            return Ok(rows);
        }

        [HttpPost]
        public async Task<ActionResult<MemberDto>> Create([FromBody] MemberDto member, CancellationToken cancellationToken)
        {
            var dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
               .ConfigureAwait(false);



            // check for case where no primary member exists in the account, such as in a newly-created account.


            int primaryCount = (await dbContext.Session.QueryAsync<int>("SELECT COUNT('Primary') FROM member WHERE ('Primary' <> 0 AND AccountUid = @AccountUid)", new { member.AccountUid })).First();
            dbContext.Commit();
            dbContext.Dispose();

            
            // create the query
            dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
               .ConfigureAwait(false);

            const string sql = @"INSERT OR IGNORE INTO member (Guid, AccountUid, LocationUid, CreatedUtc, `Primary`, JoinedDateUtc, FirstName, LastName, Address, City, Locale, PostalCode, Cancelled)
                                                 VALUES (@Guid, @AccountUid, @LocationUid, @CreatedUtc, @Primary, @JoinedDateUtc, @FirstName, @LastName, @Address, @City, @Locale, @PostalCode, @Cancelled)";

            Guid id = Guid.NewGuid();
            var builder = new SqlBuilder();
            var template = builder.AddTemplate(sql, new
            {
                Guid = id,
                member.AccountUid,
                member.LocationUid,
                CreatedUtc = DateTime.Now,
                // creates the account as Primary if and only if the Account has no other members
                Primary = primaryCount == 0,
                member.JoinedDateUtc,
                member.FirstName,
                member.LastName,
                member.Address,
                member.City,
                member.Locale,
                member.PostalCode,
                member.Cancelled
            });


            // insert the member from the template above
            var count = await dbContext.Session.ExecuteAsync(template.RawSql, template.Parameters, dbContext.Transaction)
                .ConfigureAwait(false);

            dbContext.Commit();

            // returns GUID of new member
            return count == 1 ? Ok(id) : BadRequest("Unable to add Account");

        }

        [HttpDelete("{id:Guid}")]
        public async Task<ActionResult<MemberDto>> DeleteById(Guid id, CancellationToken cancellationToken)
        {
            var dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
               .ConfigureAwait(false);

            // check for case where the current member is primary

            bool primaryCheck = (await dbContext.Session.QueryAsync<int>("SELECT COUNT(`Primary`) FROM member WHERE `Primary` <> 0 AND Guid = @Guid", new { Guid=id })).FirstOrDefault() != 0;
            int acctUid = await dbContext.Session.QueryFirstOrDefaultAsync<int>("SELECT AccountUid from member WHERE Guid = @Guid",  new { Guid=id });
            dbContext.Commit();
            await dbContext.DisposeAsync();

            int success = -1;

            if (primaryCheck)
            {
                // Reinitialize Database Context for use with  GetAccountByUID
                dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                    .ConfigureAwait(false);
                Guid acctGuid = (await AccountsController.GetAccountByUID(acctUid, dbContext)).Guid;

                // GetMembers makes its own context, so we need to terminate our previous one
                await dbContext.DisposeAsync();


                // remakes dbContext for setting a new Primary user
                dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                    .ConfigureAwait(false);

                // Grabs the next oldest user on the account and updates its Primary value
                success = await dbContext.Session.ExecuteAsync("UPDATE member SET `Primary` = 1 WHERE CreatedUtc=(SELECT MIN(CreatedUtc) FROM member WHERE AccountUID=@AccountUID AND `Primary`= 0);", new { AccountUID=acctUid });
                dbContext.Commit();

                // If no other member is found, abort the deletion and inform the user, preventing the last user from being deleted
                if (success != 1)
                {
                    return BadRequest("Unable to set primary member, member not deleted");
                }

                
            }

            dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                    .ConfigureAwait(false);

            // Delete the user
            int count = await dbContext.Session.ExecuteAsync("DELETE FROM member WHERE Guid = @Guid",  new { Guid=id });
            dbContext.Commit();


            if (count != 1)
            {
                return BadRequest("Unable to delete member");
            }

            return Ok(count);
        }
        


    }

    public class MemberDto
    {
        public Guid? Guid { get; set; }
        public int AccountUid { get; set; }

        public int LocationUid { get; set; }

        public byte Primary { get; set; }

        public DateTime? JoinedDateUtc { get; set; }

        public DateTime? CancelDateUtc { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Address { get; set; }

        public string City { get; set; }

        public string Locale { get; set; }

        public string PostalCode { get; set; }

        public byte? Cancelled { get; set; }

        public AccountsController.AccountsDto Account { get; set; }
    }
}