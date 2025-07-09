using Microsoft.Xrm.Sdk;

namespace DataverseQuery.Tests
{
    // Minimal stub for Entity for testing
    public class TestEntity : Entity
    {
        public static string EntityLogicalName => "testentity";

        public string Name { get; set; }

        public int StateCode { get; set; }

        public Guid TestEntityId { get; set; }
    }
}