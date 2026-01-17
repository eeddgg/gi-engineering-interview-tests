using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Test1.Contracts;
using Test1.Core;
using Test1.Models;
using System.Runtime.ConstrainedExecution;
using System.Data;

namespace Test1.Controllers{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountsController : ControllerBase{

        private readonly ISessionFactory _sessionFactory;
        public AccountsController(ISessionFactory sessionFactory)
        {
            _sessionFactory = sessionFactory;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AccountsDto>>> List(CancellationToken cancellationToken)
        {
            await using var dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                .ConfigureAwait(false);

            // Using dapper's One to Many SplitOn to collect location information for the account in the same query.
            const string sql = @"SELECT a.Guid, Status, AccountType, PaymentAmount, PendCancel,  PeriodStartUtc, PeriodEndUtc, NextBillingUtc, LocationUid, l.Guid, l.Name, l.Address, l.City, l.PostalCode
            FROM account a
            INNER JOIN location l ON a.LocationUID=l.UID";

            var rows = await dbContext.Session.QueryAsync<AccountsDto, LocationsController.LocationDto, AccountsDto>(sql, (account, location) => {
                account.location=location;
                return account;
            }, splitOn: "LocationUID").ConfigureAwait(false);

            dbContext.Commit();

            return Ok(rows);

        }
        
        [HttpGet("{id:Guid}")]
        public async Task<ActionResult<IEnumerable<AccountsDto>>> GetByID(Guid id, CancellationToken cancellationToken)
        {
            await using var dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                            .ConfigureAwait(false);

            IEnumerable<AccountsDto> rows = await QueryAccountByGUID(id, cancellationToken, dbContext).ConfigureAwait(false);

            return Ok(rows.FirstOrDefault());

        }

        // since getting members requires getting the account, I pulled this into its own function
        internal async Task<IEnumerable<AccountsDto>> QueryAccountByGUID(Guid id, CancellationToken cancellationToken, DapperDbContext dbContext)
        {

            // using the same Dapper relationship trick to get the account and location information in  a single query
            string sql = @"SELECT a.UID, a.Guid, Status, EndDateUtc, 
                AccountType, PaymentAmount, PendCancel, 
                PendCancelDateUtc, PeriodStartUtc, PeriodEndUtc, NextBillingUtc, 
                LocationUid, l.Guid, l.Name, l.Address, l.City, l.PostalCode
            FROM account a
            INNER JOIN location l ON a.LocationUID=l.UID 
            WHERE a.Guid = '" + id + "'";

            var rows = await dbContext.Session.QueryAsync<AccountsDto, LocationsController.LocationDto, AccountsDto>(sql, (account, location) =>
            {
                account.location = location;
                return account;
            }, splitOn: "LocationUID").ConfigureAwait(false);

            dbContext.Commit();
            await dbContext.DisposeAsync();
            return rows;
        }

        internal static async Task<AccountsDto> GetAccountByUID(int id, DapperDbContext dbContext)
        {
            string sql = @"SELECT a.UID, a.Guid, Status, EndDateUtc, 
                AccountType, PaymentAmount, PendCancel, 
                PendCancelDateUtc, PeriodStartUtc, PeriodEndUtc, NextBillingUtc, 
                LocationUid, l.Guid, l.Name, l.Address, l.City, l.PostalCode
            FROM account a
            INNER JOIN location l ON a.LocationUID=l.UID 
            WHERE a.UID = '" + id + "'";

            var rows = await dbContext.Session.QueryAsync<AccountsDto, LocationsController.LocationDto, AccountsDto>(sql, (account, location) =>
            {
                account.location = location;
                return account;
            }, splitOn: "LocationUID").ConfigureAwait(false);

            dbContext.Commit();
            await dbContext.DisposeAsync();
            return rows.FirstOrDefault();
        }

        [HttpPost]
        public async Task<ActionResult<AccountsDto>> Create([FromBody] AccountsDto account, CancellationToken cancellationToken)
        {
            await using var dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
               .ConfigureAwait(false);

            const string sql = @"INSERT OR IGNORE INTO account 
            (LocationUid, Guid, CreatedUtc, Status, AccountType, PendCancel, 
                PeriodStartUtc, PeriodEndUtc, NextBillingUtc)
            VALUES (@LocationUid, @Guid, @CreatedUtc, @Status, @accountType, 
                @PendCancel, @PeriodStartUtc, @PeriodEndUtc, @NextBillingUtc);";
            Guid id = Guid.NewGuid();
            var builder = new SqlBuilder();
            var template = builder.AddTemplate(sql, new
            {
                Guid = id,
                account.LocationUid,
                CreatedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"),
                accountType = account.AccountType,
                account.Status,
                account.PendCancel,
                PeriodStartUtc=account.PeriodStartUtc.ToString("yyyy-MM-ddTHH:mm:ss"),
                PeriodEndUtc=account.PeriodEndUtc.ToString("yyyy-MM-ddTHH:mm:ss"),
                NextBillingUtc=account.NextBillingUtc.ToString("yyyy-MM-ddTHH:mm:ss")

            });

            var count = await dbContext.Session.ExecuteAsync(template.RawSql, template.Parameters, dbContext.Transaction)
                .ConfigureAwait(false);

            dbContext.Commit();

            return count == 1 ? Ok(id) : BadRequest("Unable to add Account");
        }

        [HttpDelete("{id:Guid}")]
        public async Task<ActionResult<AccountsDto>> DeleteById(Guid id, CancellationToken cancellationToken)
        {
            await using var dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                .ConfigureAwait(false);

            const string sql = "DELETE FROM account WHERE Guid = @Guid;";

            var builder = new SqlBuilder();
            var template = builder.AddTemplate(sql, new { Guid = id });

            var count = await dbContext.Session.ExecuteAsync(template.RawSql, template.Parameters, dbContext.Transaction);

            dbContext.Commit();

            return count == 1 ? Ok(count) : BadRequest("Unable to delete account");

        }

        [HttpPatch]
        public async Task<ActionResult<AccountsDto>> UpdateByGuid([FromBody] AccountsDto account, CancellationToken cancellationToken)
        {
            await using var dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                .ConfigureAwait(false);
            //Coalescing is used to prevent updates to data when the 
            // provided object does not contain a value for a field
            const string sql = @"UPDATE account SET 
                UpdatedUtc = @UpdatedUtc, 
                Status = COALESCE(@Status, Status), 
                EndDateUtc = COALESCE(@EndDateUtc, EndDateUtc),
                AccountType = COALESCE(@AccountType, AccountType),
                PaymentAmount = COALESCE(@PaymentAmount, PaymentAmount),
                PendCancel = COALESCE(@PendCancel, PendCancel),
                PendCancelDateUtc = COALESCE(@PendCancelDateUtc, PendCancelDateUtc),
                PeriodStartUtc = COALESCE(@PeriodStartUtc, PeriodStartUtc),
                PeriodEndUtc = COALESCE(@PeriodEndUtc, PeriodEndUtc),
                NextBillingUtc = COALESCE(@NextBillingUtc, NextBillingUtc)
            WHERE Guid = @Guid";

            var builder = new SqlBuilder();
            var template = builder.AddTemplate(sql, new
            {

                UpdatedUtc = DateTime.UtcNow,
                account.Status,
                account.EndDateUtc,
                account.AccountType,
                account.PaymentAmount,
                account.PendCancel,
                account.PendCancelDateUtc,
                account.PeriodStartUtc,
                account.PeriodEndUtc,
                account.NextBillingUtc,
                account.Guid

            });
            var count = await dbContext.Session.ExecuteAsync(template.RawSql, template.Parameters, dbContext.Transaction);
            dbContext.Commit();
            return count == 1 ? Ok(count) : BadRequest("Unable to update account");
        }

        [HttpGet("{id:Guid}/members")]
        public async Task<ActionResult<IEnumerable<MemberDto>>> AccountMembers(Guid id, CancellationToken cancellationToken) {
            var dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                .ConfigureAwait(false);

            // get account information
            var tempAccountList = await QueryAccountByGUID(id, cancellationToken, dbContext).ConfigureAwait(false);
            var tempAccount = tempAccountList.FirstOrDefault();

            await dbContext.DisposeAsync();

            // reinitialize dbContext after committing transaction
            dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
                .ConfigureAwait(false);

            // I recommend not using reserved keywords as column identifiers, as it leads to errors during development
            const string sql = @"SELECT 
                Guid, AccountUid, LocationUid, `Primary`, 
                JoinedDateUtc, CancelDateUtc, FirstName, LastName, 
                Address, City, Locale, PostalCode, Cancelled
            FROM member m WHERE AccountUid = @acctUid";

            var builder = new SqlBuilder();
            var template = builder.AddTemplate(sql, new
            { acctUid = tempAccount.UID });

            // get all members associated with the account
            var rows = await dbContext.Session.QueryAsync<MemberDto>(template.RawSql, template.Parameters, dbContext.Transaction).ConfigureAwait(false);

            dbContext.Commit();

            dbContext.Dispose();

            foreach (MemberDto row in rows)
            {
                row.account = tempAccount;
            }

            return Ok(rows);
        }
        public class AccountsDto
        {
            public Int32? UID { get; set; }
            public Guid Guid { get; set; }

            public Int32? LocationUid { get; set; }

            public DateTime? EndDateUtc { get; set; }
            public AccountStatusType? Status { get; set; }
            public AccountType? AccountType { get; set; }
            public double? PaymentAmount { get; set; }
            public bool? PendCancel { get; set; }
            public DateTime? PendCancelDateUtc { get; set; }
            public DateTime PeriodStartUtc { get; set; }
            public DateTime PeriodEndUtc { get; set; }
            public DateTime NextBillingUtc { get; set; }
            public LocationsController.LocationDto location { get; set; }
        }

    }
}