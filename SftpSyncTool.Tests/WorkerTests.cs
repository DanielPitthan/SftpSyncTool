using CopyToSFTPObserver;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Configurations;
using Models.MappingTasks;
using Moq;
using Xunit;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
namespace SftpSyncTool.Tests
{
    public class WorkerTests
    {
        private readonly Mock<ILogger<Worker>> _loggerMock;
        private readonly Mock<IOptions<AppSettings>> _appSettingsMock;
        private readonly Mock<IAppTaskMapperConfigurator> _appTaskMapperConfiguratorMock;

        public WorkerTests()
        {
            _loggerMock = new Mock<ILogger<Worker>>();
            _appSettingsMock = new Mock<IOptions<AppSettings>>();
            _appTaskMapperConfiguratorMock = new Mock<IAppTaskMapperConfigurator>();
        }

        [Fact]
        public async Task StartAsync_ShouldLogInformation()
        {
            // Arrange
            var appSettings = new AppSettings { IntervaloEntreExecucoes = 1000 };
            _appSettingsMock.Setup(a => a.Value).Returns(appSettings);

            var worker = new Worker(_loggerMock.Object, _appSettingsMock.Object, _appTaskMapperConfiguratorMock.Object);

            // Act
            await worker.StartAsync(CancellationToken.None);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Serviço em execução")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task StopAsync_ShouldLogInformation()
        {
            // Arrange
            var appSettings = new AppSettings { IntervaloEntreExecucoes = 1000 };
            _appSettingsMock.Setup(a => a.Value).Returns(appSettings);

            var worker = new Worker(_loggerMock.Object, _appSettingsMock.Object, _appTaskMapperConfiguratorMock.Object);

            // Act
            await worker.StopAsync(CancellationToken.None);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Serviço em parada")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task ExecuteAsync_ShouldLogWarningWhenNoTasksMapped()
        {
            // Arrange
            var appSettings = new AppSettings { IntervaloEntreExecucoes = 1000 };
            _appSettingsMock.Setup(a => a.Value).Returns(appSettings);

            // Setup to return null (no tasks mapped)
            _appTaskMapperConfiguratorMock.Setup(m => m.MapAppTask()).Returns((AppTask)null);

            var worker = new Worker(_loggerMock.Object, _appSettingsMock.Object, _appTaskMapperConfiguratorMock.Object);

            // Act
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(1)); // Cancel after 1 second
            
            await worker.StartAsync(CancellationToken.None);
            
            // Wait for ExecuteAsync to run
            await Task.Delay(500);
            
            await worker.StopAsync(CancellationToken.None);

            // Assert - verify that the warning log was called
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Processo finalizado sem ações a serem executadas")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task ExecuteAsync_ShouldLogInformationWhenTasksAreMapped()
        {
            // Arrange
            var appSettings = new AppSettings { IntervaloEntreExecucoes = 1000 };
            _appSettingsMock.Setup(a => a.Value).Returns(appSettings);

            var appTask = new AppTask
            {
                Name = "Test Task",
                Version = "1.0",
                FolderMaps = new List<FolderMap>
                {
                    new FolderMap
                    {
                        Name = "Test Folder",
                        FolderPathOrigin = "C:\\Test",
                        SFTPPathDestination = "/test",
                        TasksMaps = new List<TasksMap>
                        {
                            new TasksMap { Name = "Task 1", Order = 1, Task = "copy:test" }
                        }
                    }
                }
            };

            _appTaskMapperConfiguratorMock.Setup(m => m.MapAppTask()).Returns(appTask);

            var worker = new Worker(_loggerMock.Object, _appSettingsMock.Object, _appTaskMapperConfiguratorMock.Object);

            // Act
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(500)); // Cancel quickly
            
            await worker.StartAsync(CancellationToken.None);
            
            // Wait briefly for ExecuteAsync to start
            await Task.Delay(200);
            
            await worker.StopAsync(CancellationToken.None);

            // Assert - verify that the information log was called with the task name and version
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Executando") && v.ToString().Contains("Test Task")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
                Times.AtLeastOnce
            );
        }
    }
}