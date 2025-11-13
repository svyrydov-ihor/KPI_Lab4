using Moq;
using Subscription_Service.Services.Interfaces;
using Subscription_Service.Models;
using Subscription_Service.Services;

namespace SubscriptionServiceTests
{
    public class MemberServiceTests
    {
        private readonly Mock<IMemberRepository> _repo = new();
        private readonly Mock<IPaymentService> _payment = new();
        private readonly Mock<INotificationService> _notify = new();
        private readonly MemberService _service;

        public MemberServiceTests()
        {
            _service = new MemberService(_repo.Object);
        }

        /// <summary>
        /// IsActive:
        /// Verifies that IMemberRepository.GetById is called
        /// when MemberService.IsActive is called
        /// </summary>
        [Fact]
        public void IsActive_ShouldCallGetById_WhenCalled()
        {
            var member = new Member { Id = 1, Name = "Oleksandr", IsActive = true};
            _repo.Setup(x => x.GetById(1)).Returns(member);
            
            var result = _service.IsActive(1);
            
            Assert.True(result);
            _repo.Verify(x => x.GetById(1), Times.Once);
        }
    }
}
