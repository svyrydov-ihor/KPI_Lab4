using Moq;
using Subscription_Service.Models;
using Subscription_Service.Services;
using Subscription_Service.Services.Interfaces;

namespace SubscriptionServiceTests
{
    public class SubscriptionServiceTests
    {
        private readonly Mock<IMemberRepository> _repo = new();
        private readonly Mock<IPaymentService> _payment = new();
        private readonly Mock<INotificationService> _notify = new();
        private readonly SubscriptionService _service;

        public SubscriptionServiceTests()
        {
            _service = new SubscriptionService(_repo.Object, _payment.Object, _notify.Object);
        }

        /// <summary>
        /// RenewSubscription:
        /// Member.SubscriptionEnd should increase
        /// when the payment is verified by IPaymentService
        /// </summary>
        [Fact]
        public void RenewSubscription_ShouldIncreaseSubscriptionEnd_WhenPaymentIsVerified()
        {
            
            var existingMember = new Member { Id = 1, Name = "Oleksandr", IsActive = true, SubscriptionEnd = DateTime.Now};
            _repo.Setup(x => x.GetById(1)).Returns(existingMember);
            _payment.Setup(x => x.VerifyPayment(1, 5)).Returns(true);
            
            bool result = _service.RenewSubscription(1, 5, 10);
            var expectedTime = DateTime.Now.AddDays(10);
            var tolerance = TimeSpan.FromSeconds(1);
            
            Assert.True(result);
            _repo.Verify(x => x.Update(existingMember), Times.Once);
            Assert.True(expectedTime - existingMember.SubscriptionEnd < tolerance);
        }
        
        /// <summary>
        /// RenewSubscription:
        /// Member.SubscriptionEnd should not increase
        /// when the payment is not verified by IPaymentService
        /// </summary>
        [Fact]
        public void RenewSubscription_ShouldNotIncreaseSubscriptionEnd_WhenPaymentIsNotVerified()
        {
            var initTime = DateTime.Now;
            var existingMember = new Member { Id = 1, Name = "Oleksandr", IsActive = true, SubscriptionEnd = initTime};
            _repo.Setup(x => x.GetById(1)).Returns(existingMember);
            _payment.Setup(x => x.VerifyPayment(1, 5)).Returns(false);
            
            bool result = _service.RenewSubscription(1, 5, 10);
            
            Assert.False(result);
            Assert.Equal(existingMember.SubscriptionEnd, initTime);
            _payment.Verify(x => x.VerifyPayment(1, 5), Times.Once);
        }
        
        /// <summary>
        /// RenewSubscription:
        /// should throw an ArgumentException
        /// when the member is not found by IMemberRepository
        /// </summary>
        [Fact]
        public void RenewSubscription_ShouldThrowException_WhenMemberNotFound()
        {
            _repo.Setup(x => x.GetById(1)).Returns((Member)null);
            
            Assert.Throws<ArgumentException>(() => _service.RenewSubscription(1, 5, 10));
        }
        
        /// <summary>
        /// RenewSubscription:
        /// IPaymentService.SendNotification should be called
        /// when renewal succeeds
        /// </summary>
        [Fact]
        public void RenewSubscription_ShouldSendNotification_WhenRenewalSucceeds()
        {
            var existingMember = new Member { Id = 1, Name = "Oleksandr", IsActive = true, SubscriptionEnd = DateTime.Now};
            _repo.Setup(x => x.GetById(1)).Returns(existingMember);
            _payment.Setup(x => x.VerifyPayment(1, 5)).Returns(true);
            
            bool result = _service.RenewSubscription(1, 5, 10);
            
            Assert.True(result);
            _notify.Verify(x => x.SendNotification("Subscription renewed!", 1), Times.Once);
        }
        
        
        /// <summary>
        /// RenewSubscription:
        /// should return false,
        /// IPaymentService.SendNotification should not be called
        /// when the invalid days parameter is passed
        /// </summary>
        /// <param name="days"></param>
        /// <param name="paymentAmount"></param>
        [Theory]
        [InlineData(0, 5)]
        [InlineData(-1, 5)]
        public void RenewSubscription_ShouldReturnFalse_WhenInvalidDaysParameter(int days, decimal paymentAmount)
        {
            var existingMember = new Member { Id = 1, Name = "Oleksandr", IsActive = true, SubscriptionEnd = DateTime.Now};
            _repo.Setup(x => x.GetById(1)).Returns(existingMember);
            _payment.Setup(x => x.VerifyPayment(1, paymentAmount)).Returns(true);
            
            bool result = _service.RenewSubscription(1, paymentAmount, days);
            
            // or exception could be thrown
            Assert.False(result);
            _notify.Verify(x => x.SendNotification("Subscription renewed!", 1), Times.Never);
        }
        
        /// <summary>
        /// DeactivateExpiredMembers:
        /// Member.IsActive should not be set to false
        /// when the member is not expired (Member.SubscriptionEnd is more than DateTime.Now)
        /// </summary>
        [Fact]
        public void DeactivateExpiredMembers_ShouldNotDeactivate_WhenMemberIsNotExpired()
        {
            var members = new List<Member>
            {
                // not expiring member
                new Member { Id = 1, Name = "Oleksandr", IsActive = true, SubscriptionEnd = DateTime.Now.AddDays(1)},
                // expiring members
                new Member { Id = 2, Name = "Vlad", IsActive = true, SubscriptionEnd = DateTime.Now.AddDays(-1)},
                new Member { Id = 3, Name = "Egor", IsActive = true, SubscriptionEnd = DateTime.Now.AddDays(-2)}
            };
            _repo.Setup(x => x.GetAll()).Returns(members);
            
            _service.DeactivateExpiredMembers();
            
            Assert.Contains(true, _repo.Object.GetAll().Select(x => x.IsActive));
        }
        
        /// <summary>
        /// DeactivateExpiredMembers:
        /// INotificationService.SendNotification should be called N times
        /// when there are N expired members (Member.SubscriptionEnd is less than DateTime.Now)
        /// </summary>
        [Fact]
        public void DeactivateExpiredMembers_ShouldNotifyNTimes_WhenThereAreNExpiredMembers()
        {
            var members = new List<Member>
            {
                // not expiring member
                new Member { Id = 1, Name = "Oleksandr", IsActive = true, SubscriptionEnd = DateTime.Now.AddDays(1)},
                // expiring members
                new Member { Id = 2, Name = "Vlad", IsActive = true, SubscriptionEnd = DateTime.Now.AddDays(-1)},
                new Member { Id = 3, Name = "Egor", IsActive = true, SubscriptionEnd = DateTime.Now.AddDays(-2)}
            };
            _repo.Setup(x => x.GetAll()).Returns(members);
            
            _service.DeactivateExpiredMembers();
            
            _notify.Verify(x => x.SendNotification(
                It.Is<string>(s => s.Contains("expired")),
                It.IsAny<int>()), Times.Exactly(2));
        }
        
        /// <summary>
        /// DeactivateExpiredMembers:
        /// INotificationService.SendNotification should never be called
        /// when there are no expired members (Member.SubscriptionEnd is more than DateTime.Now)
        /// </summary>
        [Fact]
        public void DeactivateExpiredMembers_ShouldNotNotify_WhenThereAreNoExpiredMembers()
        {
            var members = new List<Member>
            {
                // not expiring members
                new Member { Id = 1, Name = "Oleksandr", IsActive = true, SubscriptionEnd = DateTime.Now.AddDays(1)},
                new Member { Id = 2, Name = "Vlad", IsActive = true, SubscriptionEnd = DateTime.Now.AddDays(2)}
            };
            _repo.Setup(x => x.GetAll()).Returns(members);
            
            _service.DeactivateExpiredMembers();
            
            _notify.Verify(x => x.SendNotification(
                It.Is<string>(s => s.Contains("expired")),
                It.IsAny<int>()), Times.Never);
        }
        
        /// <summary>
        /// DeactivateExpiredMembers:
        /// IMemberRepository.Update should never be called
        /// when there are no members (IMemberRepository.GetAll returns an empty List)
        /// </summary>
        [Fact]
        public void DeactivateExpiredMembers_ShouldNotUpdate_WhenThereAreNoMembers()
        {
            _repo.Setup(x => x.GetAll()).Returns(new List<Member>());
            
            _service.DeactivateExpiredMembers();
            
            _repo.Verify(x => x.Update(It.IsAny<Member>()), Times.Never);
        }
    }
}
