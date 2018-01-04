namespace Khala.EventSourcing.Azure
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Khala.FakeDomain;
    using Khala.Messaging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Microsoft.WindowsAzure.Storage.RetryPolicies;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.AutoMoq;
    using Ploeh.AutoFixture.Idioms;

    [TestClass]
    public class AzureMementoStore_specs
    {
        private static CloudStorageAccount s_storageAccount;
        private static CloudBlobContainer s_container;
        private static bool s_storageEmulatorConnected;
        private IFixture _fixture;
        private IMessageSerializer _serializer;
        private AzureMementoStore _sut;

        public TestContext TestContext { get; set; }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            try
            {
                s_storageAccount = CloudStorageAccount.DevelopmentStorageAccount;
                CloudBlobClient tableClient = s_storageAccount.CreateCloudBlobClient();
                s_container = tableClient.GetContainerReference("test-memento-store");
                s_container.DeleteIfExists(options: new BlobRequestOptions { RetryPolicy = new NoRetry() });
                s_container.Create();
                s_storageEmulatorConnected = true;
            }
            catch (StorageException exception)
            when (exception.InnerException is WebException)
            {
                context.WriteLine("{0}", exception);
            }
        }

        [TestInitialize]
        public void TestInitialize()
        {
            if (s_storageEmulatorConnected == false)
            {
                Assert.Inconclusive("Could not connect to Azure Storage Emulator. See the output for details. Refer to the following URL for more information: http://go.microsoft.com/fwlink/?LinkId=392237");
            }

            _fixture = new Fixture().Customize(new AutoMoqCustomization());
            _serializer = new JsonMessageSerializer();
            _sut = new AzureMementoStore(s_container, _serializer);
        }

        [TestMethod]
        public void sut_implements_IMementoStore()
        {
            _sut.Should().BeAssignableTo<IMementoStore>();
        }

        [TestMethod]
        public void class_has_guard_clauses()
        {
            var assertion = new GuardClauseAssertion(_fixture);
            assertion.Verify(typeof(AzureMementoStore));
        }

        [TestMethod]
        public void GetMementoBlobName_returns_correctly_structured_name()
        {
            var userId = Guid.NewGuid();
            string s = userId.ToString();

            string actual =
                AzureMementoStore.GetMementoBlobName<FakeUser>(userId);

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
            FakeUserMemento memento = _fixture.Create<FakeUserMemento>();

            // Act
            await _sut.Save<FakeUser>(userId, memento, CancellationToken.None);

            // Assert
            CloudBlockBlob blob = s_container.GetBlockBlobReference(
                AzureMementoStore.GetMementoBlobName<FakeUser>(userId));
            blob.Exists().Should().BeTrue();
            using (Stream s = await blob.OpenReadAsync())
            using (var reader = new StreamReader(s))
            {
                string json = await reader.ReadToEndAsync();
                object actual = _serializer.Deserialize(json);
                actual.Should().BeOfType<FakeUserMemento>();
                actual.ShouldBeEquivalentTo(memento);
            }
        }

        [TestMethod]
        public async Task Save_overwrites_memento_blob_if_already_exists()
        {
            // Arrange
            var userId = Guid.NewGuid();
            FakeUserMemento oldMemento = _fixture.Create<FakeUserMemento>();

            CloudBlockBlob blob = s_container.GetBlockBlobReference(
                AzureMementoStore.GetMementoBlobName<FakeUser>(userId));
            await blob.UploadTextAsync(_serializer.Serialize(oldMemento));

            FakeUserMemento memento = _fixture.Create<FakeUserMemento>();

            // Act
            Func<Task> action = () => _sut.Save<FakeUser>(userId, memento, CancellationToken.None);

            // Assert
            action.ShouldNotThrow();
            blob.Exists().Should().BeTrue();
            using (Stream s = await blob.OpenReadAsync())
            using (var reader = new StreamReader(s))
            {
                string json = await reader.ReadToEndAsync();
                object actual = _serializer.Deserialize(json);
                actual.Should().BeOfType<FakeUserMemento>();
                actual.ShouldBeEquivalentTo(memento);
            }
        }

        [TestMethod]
        public async Task Save_sets_ContentType_to_application_json()
        {
            var userId = Guid.NewGuid();
            FakeUserMemento memento = _fixture.Create<FakeUserMemento>();

            await _sut.Save<FakeUser>(userId, memento, CancellationToken.None);

            string blobName =
                AzureMementoStore.GetMementoBlobName<FakeUser>(userId);
            ICloudBlob blob = await
                s_container.GetBlobReferenceFromServerAsync(blobName);
            blob.Properties.ContentType.Should().Be("application/json");
        }

        [TestMethod]
        public async Task Find_restores_memento_correctly()
        {
            // Arrange
            var userId = Guid.NewGuid();
            FakeUserMemento memento = _fixture.Create<FakeUserMemento>();
            await _sut.Save<FakeUser>(userId, memento, CancellationToken.None);

            // Act
            IMemento actual = await _sut.Find<FakeUser>(userId, CancellationToken.None);

            // Assert
            actual.Should().BeOfType<FakeUserMemento>();
            actual.ShouldBeEquivalentTo(memento);
        }

        [TestMethod]
        public async Task Find_returns_null_if_blob_not_found()
        {
            var userId = Guid.NewGuid();
            IMemento actual = await _sut.Find<FakeUser>(userId, CancellationToken.None);
            actual.Should().BeNull();
        }

        [TestMethod]
        public async Task Delete_deletes_memento_blob()
        {
            var userId = Guid.NewGuid();
            FakeUserMemento memento = _fixture.Create<FakeUserMemento>();
            await _sut.Save<FakeUser>(userId, memento, CancellationToken.None);

            await _sut.Delete<FakeUser>(userId, CancellationToken.None);

            CloudBlockBlob blob = s_container.GetBlockBlobReference(
                AzureMementoStore.GetMementoBlobName<FakeUser>(userId));
            blob.Exists().Should().BeFalse();
        }

        [TestMethod]
        public void Delete_does_not_fails_even_if_memento_not_found()
        {
            var userId = Guid.NewGuid();
            Func<Task> action = () => _sut.Delete<FakeUser>(userId, CancellationToken.None);
            action.ShouldNotThrow();
        }
    }
}
