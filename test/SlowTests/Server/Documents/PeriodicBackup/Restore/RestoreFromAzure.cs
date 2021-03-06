﻿using System;
using System.Linq;
using FastTests;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Operations.Backups;
using Raven.Client.Exceptions;
using Raven.Client.ServerWide.Operations;
using Raven.Server.Documents;
using Raven.Server.Documents.PeriodicBackup.Azure;
using Raven.Server.ServerWide.Context;
using Raven.Tests.Core.Utils.Entities;
using Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SlowTests.Server.Documents.PeriodicBackup.Restore
{
    public class RestoreFromAzure : RavenTestBase
    {
        public RestoreFromAzure(ITestOutputHelper output) : base(output)
        {
        }

        [Fact, Trait("Category", "Smuggler")]
        public void restore_azure_cloud_settings_tests()
        {
            var backupPath = NewDataPath(suffix: "BackupFolder");
            using (var store = GetDocumentStore(new Options
            {
                ModifyDatabaseName = s => $"{s}_2"
            }))
            {
                var databaseName = $"restored_database-{Guid.NewGuid()}";
                var restoreConfiguration = new RestoreFromAzureConfiguration
                {
                    DatabaseName = databaseName
                };

                var restoreBackupTask = new RestoreBackupOperation(restoreConfiguration);

                var e = Assert.Throws<RavenException>(() => store.Maintenance.Server.Send(restoreBackupTask));
                Assert.Contains("Account Key cannot be null or empty", e.InnerException.Message);

                restoreConfiguration.Settings.AccountKey = "test";
                restoreBackupTask = new RestoreBackupOperation(restoreConfiguration);
                e = Assert.Throws<RavenException>(() => store.Maintenance.Server.Send(restoreBackupTask));
                Assert.Contains("Account Name cannot be null or empty", e.InnerException.Message);

                restoreConfiguration.Settings.AccountName = "test";
                restoreBackupTask = new RestoreBackupOperation(restoreConfiguration);
                e = Assert.Throws<RavenException>(() => store.Maintenance.Server.Send(restoreBackupTask));
                Assert.Contains("Storage Container cannot be null or empty", e.InnerException.Message);
            }
        }

        [AzureStorageEmulatorFact]
        public void can_backup_and_restore()
        {
            var azureSettings = GenerateAzureSettings();
            InitContainer(azureSettings);

            using (var store = GetDocumentStore())
            {

                using (var session = store.OpenSession())
                {
                    session.Store(new User { Name = "oren" }, "users/1");
                    session.CountersFor("users/1").Increment("likes", 100);
                    session.SaveChanges();
                }

                var config = new PeriodicBackupConfiguration
                {
                    BackupType = BackupType.Backup,
                    AzureSettings = azureSettings,
                    IncrementalBackupFrequency = "0 0 1 1 *"
                };

                var backupTaskId = (store.Maintenance.Send(new UpdatePeriodicBackupOperation(config))).TaskId;
                store.Maintenance.Send(new StartBackupOperation(true, backupTaskId));
                var operation = new GetPeriodicBackupStatusOperation(backupTaskId);
                PeriodicBackupStatus status = null;
                var value = WaitForValue(() =>
                {
                    status = store.Maintenance.Send(operation).Status;
                    return status?.LastEtag;
                }, 4);
                Assert.True(4 == value, $"4 == value, Got status: {status != null}, exception: {status?.Error?.Exception}");
                Assert.True(status.LastOperationId != null, $"status.LastOperationId != null, Got status: {status != null}, exception: {status?.Error?.Exception}");

                var backupOperation = store.Maintenance.Send(new GetOperationStateOperation(status.LastOperationId.Value));

                var backupResult = backupOperation.Result as BackupResult;
                Assert.True(backupResult != null && backupResult.Counters.Processed, "backupResult != null && backupResult.Counters.Processed");
                Assert.True(1 == backupResult.Counters.ReadCount, "1 == backupResult.Counters.ReadCount");

                using (var session = store.OpenSession())
                {
                    session.Store(new User { Name = "ayende" }, "users/2");
                    session.CountersFor("users/2").Increment("downloads", 200);

                    session.SaveChanges();
                }

                var lastEtag = store.Maintenance.Send(new GetStatisticsOperation()).LastDocEtag;
                store.Maintenance.Send(new StartBackupOperation(false, backupTaskId));
                value = WaitForValue(() => store.Maintenance.Send(operation).Status.LastEtag, lastEtag);
                Assert.Equal(lastEtag, value);

                // restore the database with a different name
                var databaseName = $"restored_database-{Guid.NewGuid()}";

                azureSettings.RemoteFolderName = status.FolderName;
                var restoreFromGoogleCloudConfiguration = new RestoreFromAzureConfiguration()
                {
                    DatabaseName = databaseName,
                    Settings = azureSettings,
                    DisableOngoingTasks = true
                };
                var googleCloudOperation = new RestoreBackupOperation(restoreFromGoogleCloudConfiguration);
                var restoreOperation = store.Maintenance.Server.Send(googleCloudOperation);

                restoreOperation.WaitForCompletion(TimeSpan.FromSeconds(30));
                using (var store2 = GetDocumentStore(new Options()
                {
                    CreateDatabase = false,
                    ModifyDatabaseName = s => databaseName
                }))
                {
                    using (var session = store2.OpenSession(databaseName))
                    {
                        var users = session.Load<User>(new[] { "users/1", "users/2" });
                        Assert.True(users.Any(x => x.Value.Name == "oren"));
                        Assert.True(users.Any(x => x.Value.Name == "ayende"));

                        var val = session.CountersFor("users/1").Get("likes");
                        Assert.Equal(100, val);
                        val = session.CountersFor("users/2").Get("downloads");
                        Assert.Equal(200, val);
                    }

                    var originalDatabase = Server.ServerStore.DatabasesLandlord.TryGetOrCreateResourceStore(store.Database).Result;
                    var restoredDatabase = Server.ServerStore.DatabasesLandlord.TryGetOrCreateResourceStore(databaseName).Result;
                    using (restoredDatabase.DocumentsStorage.ContextPool.AllocateOperationContext(out DocumentsOperationContext ctx))
                    using (ctx.OpenReadTransaction())
                    {
                        var databaseChangeVector = DocumentsStorage.GetDatabaseChangeVector(ctx);
                        Assert.Equal($"A:7-{originalDatabase.DbBase64Id}, A:8-{restoredDatabase.DbBase64Id}", databaseChangeVector);
                    }
                }
            }
        }

        private void InitContainer(AzureSettings azureSettings)
        {
            using (var client = new RavenAzureClient(azureSettings))
            {
                client.DeleteContainer();
                client.PutContainer();
            }
        }

        private AzureSettings GenerateAzureSettings(string containerName = "mycontainer")
        {
            return new AzureSettings
            {
                AccountName = Azure.AzureAccountName,
                AccountKey = Azure.AzureAccountKey,
                StorageContainer = containerName,
                RemoteFolderName = ""
            };
        }
    }
}
