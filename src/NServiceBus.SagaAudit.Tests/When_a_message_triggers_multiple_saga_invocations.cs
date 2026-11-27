namespace NServiceBus.SagaAudit.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Audit;
    using Extensibility;
    using NServiceBus;
    using NUnit.Framework;
    using Pipeline;
    using SagaAudit;
    using Transport;

    public class When_a_message_triggers_multiple_saga_invocations
    {
        [Test]
        public async Task It_should_store_all_ids_in_the_audit_metadata()
        {
            var processing = ProcessingStarted();
            processing.State.AddInvokedSaga(typeof(MySaga).FullName, Guid.Empty);
            processing.State.AddInvokedSaga(typeof(MySaga).FullName, Guid.Empty);

            var context = await processing.Audit().ConfigureAwait(false);

            Assert.That(context.AuditMetadata["NServiceBus.InvokedSagas"], Is.EqualTo("NServiceBus.SagaAudit.Tests.When_a_message_triggers_multiple_saga_invocations+MySaga:00000000-0000-0000-0000-000000000000;NServiceBus.SagaAudit.Tests.When_a_message_triggers_multiple_saga_invocations+MySaga:00000000-0000-0000-0000-000000000000"));
        }

        [Test]
        public async Task It_should_accumulate_all_state_changes_in_the_audit_metadata()
        {
            var firstSagaId = Guid.NewGuid();
            var secondSagaId = Guid.NewGuid();
            var processing = ProcessingStarted();
            processing.State.AddSagaStateChange(firstSagaId, "New");
            processing.State.AddSagaStateChange(secondSagaId, "Updated");
            processing.State.AddSagaStateChange(firstSagaId, "Completed");

            var context = await processing.Audit().ConfigureAwait(false);

            Assert.That(context.AuditMetadata["ServiceControl.SagaStateChange"], Is.EqualTo($"{firstSagaId}:New;{secondSagaId}:Updated;{firstSagaId}:Completed"));
        }

        [Test]
        public async Task It_should_not_write_audit_metadata_when_no_saga_was_invoked()
        {
            var context = await ProcessingStarted().Audit().ConfigureAwait(false);

            Assert.That(context.AuditMetadata, Is.Empty);
        }

        [Test]
        public async Task It_should_not_write_audit_metadata_when_state_was_not_initialized()
        {
            var context = new FakeAuditContext(new ContextBag(new ContextBag()), CancellationToken.None);

            await new AuditSagaMetadataBehavior().Invoke(context, _ => Task.CompletedTask).ConfigureAwait(false);

            Assert.That(context.AuditMetadata, Is.Empty);
        }

        [Test]
        public async Task It_should_create_a_new_state_for_each_processing_attempt()
        {
            var firstAttempt = ProcessingStarted();
            var secondAttempt = ProcessingStarted();
            firstAttempt.State.AddInvokedSaga(typeof(MySaga).FullName, Guid.Empty);

            var context = await secondAttempt.Audit().ConfigureAwait(false);

            Assert.That(context.AuditMetadata, Is.Empty);
        }

        static Processing ProcessingStarted()
        {
            var physicalContext = PhysicalContextProxy.Create();
            var nextInvoked = false;

            var invocation = new InitializeSagaAuditStateBehavior().Invoke(physicalContext, _ =>
            {
                nextInvoked = true;
                return Task.CompletedTask;
            });

            Assert.Multiple(() =>
            {
                Assert.That(invocation.IsCompletedSuccessfully, Is.True);
                Assert.That(nextInvoked, Is.True);
            });

            return new Processing(physicalContext.Extensions);
        }

        class Processing(ContextBag physicalContextExtensions)
        {
            public SagaAuditState State { get; } = new ContextBag(new ContextBag(physicalContextExtensions)).Get<SagaAuditState>();

            public async Task<FakeAuditContext> Audit(CancellationToken cancellationToken = default)
            {
                var context = new FakeAuditContext(new ContextBag(physicalContextExtensions), cancellationToken);
                await new AuditSagaMetadataBehavior().Invoke(context, _ => Task.CompletedTask).ConfigureAwait(false);
                return context;
            }
        }

        class PhysicalContextProxy : DispatchProxy
        {
            public ContextBag Extensions { get; } = new ContextBag();

            public static IIncomingPhysicalMessageContext Create() => Create<IIncomingPhysicalMessageContext, PhysicalContextProxy>();

            protected override object Invoke(MethodInfo targetMethod, object[] args) => targetMethod.Name switch
            {
                "get_Extensions" => Extensions,
                _ => throw new NotImplementedException(targetMethod.Name)
            };

        }

        class FakeAuditContext(ContextBag extensions, CancellationToken cancellationToken) : IAuditContext
        {
            public ContextBag Extensions { get; } = extensions;
            public IServiceProvider Builder => throw new NotImplementedException();
            public CancellationToken CancellationToken => cancellationToken;
            public OutgoingMessage Message { get; } = new OutgoingMessage("id", [], Array.Empty<byte>());
            public string AuditAddress => "audit";
            public TimeSpan? TimeToBeReceived => null;
            public Dictionary<string, string> AuditMetadata { get; } = [];
            public AuditAction AuditAction { get; set; }
            public IAuditActionContext PreventChanges() => throw new NotImplementedException();
        }

        class MySaga : Saga<Data>, IAmStartedByMessages<SagaStartMessage>
        {
            public MySaga()
            {
                Entity = new Data();
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<Data> mapper) =>
                mapper.MapSaga(s => s.SagaId).ToMessage<SagaStartMessage>(m => m.Id);

            public Task Handle(SagaStartMessage message, IMessageHandlerContext context) => Task.CompletedTask;
        }

        class Data : ContainSagaData
        {
            public Guid SagaId { get; set; }
        }

        class SagaStartMessage : IMessage
        {
            public Guid Id { get; set; }
        }
    }
}
