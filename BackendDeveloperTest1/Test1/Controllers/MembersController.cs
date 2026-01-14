using Microsoft.AspNetCore.Mvc;
using Dapper;
using Test1.Contracts;
using Test1.Models;

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
    }
}