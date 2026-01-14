using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Test1.Contracts;
using Test1.Core;
using Test1.Models;
using System.Runtime.ConstrainedExecution;

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

            const string sql=@"SELECT a.Guid, Status, AccountType, PaymentAmount, PendCancel,  PeriodStartUtc, PeriodEndUtc, NextBillingUtc, LocationUid, l.Guid, l.Name, l.Address, l.City, l.PostalCode
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

            string sql=@"SELECT a.Guid, Status, EndDateUtc, AccountType, PaymentAmount, PendCancel, PendCancelDateUtc, PeriodStartUtc, PeriodEndUtc, NextBillingUtc, LocationUid, l.Guid, l.Name, l.Address, l.City, l.PostalCode
            FROM account a
            INNER JOIN location l ON a.LocationUID=l.UID where a.Guid = '" + id+"'";

            var rows = await dbContext.Session.QueryAsync<AccountsDto, LocationsController.LocationDto, AccountsDto>(sql, (account, location) => {
                account.location=location;
                return account;
            }, splitOn: "LocationUID").ConfigureAwait(false);

            dbContext.Commit();

            return Ok(rows.FirstOrDefault());

        }

        [HttpPost]
        public async Task<ActionResult<AccountsDto>> Create([FromBody] AccountsDto account, CancellationToken cancellationToken)
        {
            await using var dbContext = await _sessionFactory.CreateContextAsync(cancellationToken)
               .ConfigureAwait(false);

            const string sql = @"INSERT OR IGNORE INTO account 
            (LocationUid, Guid, CreatedUtc, Status, AccountType, PendCancel, PeriodStartUtc, PeriodEndUtc, NextBillingUtc)
            VALUES (@LocationUid, @Guid, @CreatedUtc, @Status, @accountType, @PendCancel, @PeriodStartUtc, @PeriodEndUtc, @NextBillingUtc);";
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
                Status=account.Status,
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

        public class AccountsDto
        {
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