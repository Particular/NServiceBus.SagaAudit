namespace ServiceControl.Plugin.Nsb6.SagaAudit.AcceptanceTests
{
    using System;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Saga;
    using NUnit.Framework;

    public class When_queue_does_not_exist : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_skip_auditing()
        {
            var context =  Scenario.Define<Context>()
                .WithEndpoint<EndpointWithASaga>(c => c.When(ctx => ctx.EndpointsStarted, (b, ctx) => ctx.Done = true))
                .Done(c => c.Done)
                .AllowExceptions(e => false)
                .Run();

            Assert.IsTrue(context.Done);
            StringAssert.Contains("You have ServiceControl plugins installed in your endpoint, however, this endpoint is unable to contact the ServiceControl Backend to report endpoint information", context.Exceptions);
        }
        
        class EndpointWithASaga : EndpointConfigurationBuilder
        {
            public EndpointWithASaga()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.AuditSagaStateChanges(Address.Parse("InvalidAddress"));
                });
            }

            public class NotSent : IMessage
            {
                public Guid MessageId { get; set; }
            }

            public class NotStartableSaga : Saga<NotStartableSaga.MyData>, IAmStartedByMessages<NotSent>
            {
                public void Handle(NotSent message)
                {
                    throw new NotImplementedException();
                }

                public class MyData : ContainSagaData
                {
                    public virtual Guid MessageId { get; set; }
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MyData> mapper)
                {
                    mapper.ConfigureMapping<NotSent>(message => message.MessageId).ToSaga(saga => saga.MessageId);
                }
            }
        }

        class Context : ScenarioContext
        {
            public bool Done { get; set; }
            public bool Received { get; set; }
        }
    }
}