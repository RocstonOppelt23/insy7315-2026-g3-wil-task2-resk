using RESK.WIL.Models;

namespace RESK.WIL.Tests
{
    public class SearchContractTests
    {
        // Kuan-Chi's code: user search results must not expose private profile/security fields.
        [Theory]
        [InlineData("Email")]
        [InlineData("Phone")]
        [InlineData("PhysicalAddress")]
        [InlineData("IdNumber")]
        [InlineData("PasswordHash")]
        [InlineData("IsMfaEnabled")]
        [InlineData("ActiveSessions")]
        public void UserSearch_does_not_return_private_user_fields(string propertyName)
        {
            var property = typeof(UserSearch).GetProperty(propertyName);

            Assert.Null(property);
        }
    }
}
