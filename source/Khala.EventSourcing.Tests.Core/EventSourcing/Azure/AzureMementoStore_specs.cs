namespace Khala.EventSourcing.Azure
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using AutoFixture;
    using AutoFixture.AutoMoq;
    using AutoFixture.Idioms;
    using FluentAssertions;
    using Khala.FakeDomain;
    using Khala.Messaging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Microsoft.WindowsAzure.Storage.RetryPolicies;

    [TestClass]
    public class AzureMementoStore_specs
    {
        private static IMessageSerializer s_serializer;
        private static CloudBlobContainer s_container;

        public TestContext TestContext { get; set; }

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            s_serializer = new JsonMessageSerializer();

            try
            {
                CloudBlobClient tableClient = CloudStorageAccount.DevelopmentStorageAccount.CreateCloudBlobClient();
                s_container = tableClient.GetContainerReference("test-memento-store");
                await s_container.DeleteIfExistsAsync(accessCondition: default, new BlobRequestOptions { RetryPolicy = new NoRetry() }, operationContext: default);
                await s_container.CreateAsync();
            }
            catch (StorageException exception)
            {
                context.WriteLine($"{exception}");
                Assert.Inconclusive("Could not connect to Azure Storage Emulator. See the output for details. Refer to the following URL for more information: http://go.microsoft.com/fwlink/?LinkId=392237");
            }
        }

        [TestMethod]
        public void sut_implements_IMementoStore()
        {
            typeof(AzureMementoStore).Should().Implement<IMementoStore>();
        }

        [TestMethod]
        public void class_has_guard_clauses()
        {
            IFixture builder = new Fixture().Customize(new AutoMoqCustomization());
            new GuardClauseAssertion(builder).Verify(typeof(AzureMementoStore));
        }

        [TestMethod]
        public void GetMementoBlobName_returns_correctly_structured_name()
        {
            var userId = Guid.NewGuid();
            string s = userId.ToString();

            string actual = AzureMementoStore.GetMementoBlobName<FakeUser>(userId);

            TestContext.WriteLine("{0}", actual);
            string[] fragments = new[]
            {
                typeof(FakeUser).FullName,
                s.Substring(0, 2),
                s.Substring(2, 2),
                $"{s}.json",
            };
            actual.Should().Be(string.Join("/", fragments));
        }

        [TestMethod]
        public async Task Save_uploads_memento_blob_correctly()
        {
            // Arrange
            var userId = Guid.NewGuid();
            FakeUserMemento memento = new Fixture().Create<FakeUserMemento>();
            var sut = new AzureMementoStore(s_container, s_serializer);

            // Act
            await sut.Save<FakeUser>(userId, memento);

            // Assert
            CloudBlockBlob blob = s_container.GetBlockBlobReference(
                AzureMementoStore.GetMementoBlobName<FakeUser>(userId));
            (await blob.ExistsAsync()).Should().BeTrue();
            using (Stream s = await blob.OpenReadAsync())
            using (var reader = new StreamReader(s))
            {
                string json = await reader.ReadToEndAsync();
                object actual = s_serializer.Deserialize(json);
                actual.Should().BeOfType<FakeUserMemento>();
                actual.ShouldBeEquivalentTo(memento);
            }
        }

        [TestMethod]
        public async Task Save_overwrites_memento_blob_if_already_exists()
        {
            // Arrange
            var sut = new AzureMementoStore(s_container, s_serializer);
            var userId = Guid.NewGuid();
            var fixture = new Fixture();
            FakeUserMemento oldMemento = fixture.Create<FakeUserMemento>();

            CloudBlockBlob blob = s_container.GetBlockBlobReference(
                AzureMementoStore.GetMementoBlobName<FakeUser>(userId));
            await blob.UploadTextAsync(s_serializer.Serialize(oldMemento));

            FakeUserMemento memento = fixture.Create<FakeUserMemento>();

            // Act
            Func<Task> action = () => sut.Save<FakeUser>(userId, memento);

            // Assert
            action.ShouldNotThrow();
            (await blob.ExistsAsync()).Should().BeTrue();
            using (Stream s = await blob.OpenReadAsync())
            using (var reader = new StreamReader(s))
            {
                string json = await reader.ReadToEndAsync();
                object actual = s_serializer.Deserialize(json);
                actual.Should().BeOfType<FakeUserMemento>();
                actual.ShouldBeEquivalentTo(memento);
            }
        }

        [TestMethod]
        public async Task Save_sets_ContentType_to_application_json()
        {
            var sut = new AzureMementoStore(s_container, s_serializer);
            var userId = Guid.NewGuid();
            FakeUserMemento memento = new Fixture().Create<FakeUserMemento>();

            await sut.Save<FakeUser>(userId, memento);

            string blobName = AzureMementoStore.GetMementoBlobName<FakeUser>(userId);
            ICloudBlob blob = await s_container.GetBlobReferenceFromServerAsync(blobName);
            blob.Properties.ContentType.Should().Be("application/json");
        }

        [TestMethod]
        public async Task Find_restores_memento_correctly()
        {
            // Arrange
            var sut = new AzureMementoStore(s_container, s_serializer);
            var userId = Guid.NewGuid();
            FakeUserMemento memento = new Fixture().Create<FakeUserMemento>();
            await sut.Save<FakeUser>(userId, memento);

            // Act
            IMemento actual = await sut.Find<FakeUser>(userId);

            // Assert
            actual.Should().BeOfType<FakeUserMemento>();
            actual.ShouldBeEquivalentTo(memento);
        }

        [TestMethod]
        public async Task Find_returns_null_if_blob_not_found()
        {
            var sut = new AzureMementoStore(s_container, s_serializer);
            var userId = Guid.NewGuid();

            IMemento actual = await sut.Find<FakeUser>(userId);

            actual.Should().BeNull();
        }

        [TestMethod]
        public async Task Delete_deletes_memento_blob()
        {
            var sut = new AzureMementoStore(s_container, s_serializer);
            var userId = Guid.NewGuid();
            FakeUserMemento memento = new Fixture().Create<FakeUserMemento>();
            await sut.Save<FakeUser>(userId, memento);

            await sut.Delete<FakeUser>(userId);

            CloudBlockBlob blob = s_container.GetBlockBlobReference(
                AzureMementoStore.GetMementoBlobName<FakeUser>(userId));
            (await blob.ExistsAsync()).Should().BeFalse();
        }

        [TestMethod]
        public void Delete_does_not_fails_even_if_memento_not_found()
        {
            var sut = new AzureMementoStore(s_container, s_serializer);
            var userId = Guid.NewGuid();

            Func<Task> action = () => sut.Delete<FakeUser>(userId);

            action.ShouldNotThrow();
        }
    }
}
