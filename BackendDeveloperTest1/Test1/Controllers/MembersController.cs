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

            
            // get all members associated with the account
            var rows = await dbContext.Session.QueryAsync<MemberDto>(sql, dbContext.Transaction).ConfigureAwait(false);

            dbContext.Commit();

            dbContext.Dispose();

            foreach (MemberDto row in rows) {
                dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                .ConfigureAwait(false);

                var tempAccount = await AccountsController.GetAccountByUID(row.AccountUid, dbContext).ConfigureAwait(false);

                row.account = tempAccount;

            await dbContext.DisposeAsync();

            }

            return Ok(rows);
        }

    }

    public class MemberDto
    {
        public Guid Guid { get; set; }
        public int AccountUid { get; set; }

        public int LocationUid { get; set; }

        public byte Primary { get; set; }

        public DateTime JoinedDateUtc { get; set; }

        public DateTime? CancelDateUtc { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Address { get; set; }

        public string City { get; set; }

        public string Locale { get; set; }

        public string PostalCode { get; set; }

        public byte Cancelled { get; set; }

        public AccountsController.AccountsDto account { get; set; }
    }
}