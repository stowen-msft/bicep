// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Bicep.Core.FileSystem;
using Bicep.Core.UnitTests.Assertions;
using Bicep.Core.UnitTests.Utils;
using Bicep.LangServer.IntegrationTests.Helpers;
using Bicep.LanguageServer;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Bicep.LangServer.IntegrationTests
{
    // Search for bicepconfig.json in DiscoverLocalConfigurationFile(..) in ConfigHelper starts from current directory.
    // In the below tests, we'll explicitly set the current directory and disable running tests in parallel to avoid conflicts
    [TestClass]
    [DoNotParallelize]
    [SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "Test methods do not need to follow this convention.")]
    public class BicepConfigTests
    {
        [NotNull]
        public TestContext? TestContext { get; set; }
        private readonly string CurrentDirectory = Directory.GetCurrentDirectory();

        [TestMethod]
        public async Task BicepConfigFileModification_ShouldRefreshCompilation()
        {
            var fileSystemDict = new Dictionary<Uri, string>();
            var diagsListener = new MultipleMessageListener<PublishDiagnosticsParams>();
            var serverOptions = new Server.CreationOptions(FileResolver: new InMemoryFileResolver(fileSystemDict));
            var client = await IntegrationTestHelper.StartServerWithClientConnectionAsync(
                TestContext,
                options =>
                {
                    options.OnPublishDiagnostics(diags => diagsListener.AddMessage(diags));
                },
                serverOptions);

            var mainUri = DocumentUri.FromFileSystemPath("/path/to/main.bicep");
            fileSystemDict[mainUri.ToUri()] = @"param storageAccountName string = 'test'";

            string bicepConfigFileContents = @"{
  ""analyzers"": {
    ""core"": {
      ""verbose"": false,
      ""enabled"": true,
      ""rules"": {
        ""no-unused-params"": {
          ""level"": ""info""
        }
      }
    }
  }
}";

            string bicepConfigFilePath = FileHelper.SaveResultFile(TestContext, "bicepconfig.json", bicepConfigFileContents);
            Directory.SetCurrentDirectory(Path.GetDirectoryName(bicepConfigFilePath)!);
            var bicepConfigUri = DocumentUri.FromFileSystemPath(bicepConfigFilePath);

            fileSystemDict[bicepConfigUri.ToUri()] = bicepConfigFileContents;

            // open the main document and verify diagnostics
            {
                client.TextDocument.DidOpenTextDocument(TextDocumentParamHelper.CreateDidOpenDocumentParams(mainUri, fileSystemDict[mainUri.ToUri()], 1));

                var diagsParams = await diagsListener.WaitNext();
                diagsParams.Uri.Should().Be(mainUri);
                diagsParams.Diagnostics.Should().SatisfyRespectively(
                    x =>
                    {
                        x.Message.Should().Be(@"Parameter ""storageAccountName"" is declared but never used.");
                        x.Severity.Should().Be(DiagnosticSeverity.Information);
                        x.Code?.String.Should().Be("https://aka.ms/bicep/linter/no-unused-params");
                        x.Range.Should().Be(new Range
                        {
                            Start = new Position(0, 6),
                            End = new Position(0, 24)
                        });
                    });
            }

            // update bicepconfig.json and verify diagnostics
            {
                client.TextDocument.DidChangeTextDocument(TextDocumentParamHelper.CreateDidChangeTextDocumentParams(bicepConfigUri, @"{
  ""analyzers"": {
    ""core"": {
      ""verbose"": false,
      ""enabled"": true,
      ""rules"": {
        ""no-unused-params"": {
          ""level"": ""off""
        }
      }
    }
  }
}", 2));

                var diagsParams = await diagsListener.WaitNext();
                diagsParams.Uri.Should().Be(mainUri);
                diagsParams.Diagnostics.Should().BeEmpty();
            }
        }

        [TestMethod]
        public async Task BicepConfigFileDeletion_ShouldRefreshCompilation()
        {
            var fileSystemDict = new Dictionary<Uri, string>();
            var diagsListener = new MultipleMessageListener<PublishDiagnosticsParams>();
            var serverOptions = new Server.CreationOptions(FileResolver: new InMemoryFileResolver(fileSystemDict));
            var client = await IntegrationTestHelper.StartServerWithClientConnectionAsync(
                TestContext,
                options =>
                {
                    options.OnPublishDiagnostics(diags => diagsListener.AddMessage(diags));
                },
                serverOptions);

            var mainUri = DocumentUri.FromFileSystemPath("/path/to/main.bicep");
            fileSystemDict[mainUri.ToUri()] = @"param storageAccountName string = 'test'";

            string bicepConfigFileContents = @"{
  ""analyzers"": {
    ""core"": {
      ""verbose"": false,
      ""enabled"": true,
      ""rules"": {
        ""no-unused-params"": {
          ""level"": ""info""
        }
      }
    }
  }
}";

            string bicepConfigFilePath = FileHelper.SaveResultFile(TestContext, "bicepconfig.json", bicepConfigFileContents);
            Directory.SetCurrentDirectory(Path.GetDirectoryName(bicepConfigFilePath)!);
            var bicepConfigUri = DocumentUri.FromFileSystemPath(bicepConfigFilePath);

            fileSystemDict[bicepConfigUri.ToUri()] = bicepConfigFileContents;

            // open the main document and verify diagnostics
            {
                client.TextDocument.DidOpenTextDocument(TextDocumentParamHelper.CreateDidOpenDocumentParams(mainUri, fileSystemDict[mainUri.ToUri()], 1));

                var diagsParams = await diagsListener.WaitNext();
                diagsParams.Uri.Should().Be(mainUri);
                diagsParams.Diagnostics.Should().SatisfyRespectively(
                    x =>
                    {
                        x.Message.Should().Be(@"Parameter ""storageAccountName"" is declared but never used.");
                        x.Severity.Should().Be(DiagnosticSeverity.Information);
                        x.Code?.String.Should().Be("https://aka.ms/bicep/linter/no-unused-params");
                        x.Range.Should().Be(new Range
                        {
                            Start = new Position(0, 6),
                            End = new Position(0, 24)
                        });
                    });
            }

            // Delete bicepconfig.json and verify diagnostics are based off of default bicepconfig.json
            {
                File.Delete(bicepConfigFilePath);

                client.Workspace.DidChangeWatchedFiles(new DidChangeWatchedFilesParams
                {
                    Changes = new Container<FileEvent>(new FileEvent
                    {
                        Type = FileChangeType.Deleted,
                        Uri = bicepConfigUri,
                    })
                });

                var diagsParams = await diagsListener.WaitNext();
                diagsParams.Uri.Should().Be(mainUri);
                diagsParams.Diagnostics.Should().SatisfyRespectively(
                    x =>
                    {
                        x.Message.Should().Be(@"Parameter ""storageAccountName"" is declared but never used.");
                        x.Severity.Should().Be(DiagnosticSeverity.Warning);
                        x.Code?.String.Should().Be("https://aka.ms/bicep/linter/no-unused-params");
                        x.Range.Should().Be(new Range
                        {
                            Start = new Position(0, 6),
                            End = new Position(0, 24)
                        });
                    });
            }
        }

        [TestMethod]
        public async Task BicepConfigFileCreation_ShouldRefreshCompilation()
        {
            var fileSystemDict = new Dictionary<Uri, string>();
            var diagsListener = new MultipleMessageListener<PublishDiagnosticsParams>();
            var serverOptions = new Server.CreationOptions(FileResolver: new InMemoryFileResolver(fileSystemDict));
            var client = await IntegrationTestHelper.StartServerWithClientConnectionAsync(
                TestContext,
                options =>
                {
                    options.OnPublishDiagnostics(diags => diagsListener.AddMessage(diags));
                },
                serverOptions);

            var mainUri = DocumentUri.FromFileSystemPath("/path/to/main.bicep");
            fileSystemDict[mainUri.ToUri()] = @"param storageAccountName string = 'test'";

            // open the main document and verify diagnostics
            {
                client.TextDocument.DidOpenTextDocument(TextDocumentParamHelper.CreateDidOpenDocumentParams(mainUri, fileSystemDict[mainUri.ToUri()], 1));

                var diagsParams = await diagsListener.WaitNext();
                diagsParams.Uri.Should().Be(mainUri);
                diagsParams.Diagnostics.Should().SatisfyRespectively(
                    x =>
                    {
                        x.Message.Should().Be(@"Parameter ""storageAccountName"" is declared but never used.");
                        x.Severity.Should().Be(DiagnosticSeverity.Warning);
                        x.Code?.String.Should().Be("https://aka.ms/bicep/linter/no-unused-params");
                        x.Range.Should().Be(new Range
                        {
                            Start = new Position(0, 6),
                            End = new Position(0, 24)
                        });
                    });
            }

            // Create bicepconfig.json and verify diagnostics
            {
                string bicepConfigFileContents = @"{
  ""analyzers"": {
    ""core"": {
      ""verbose"": false,
      ""enabled"": true,
      ""rules"": {
        ""no-unused-params"": {
          ""level"": ""info""
        }
      }
    }
  }
}";

                string bicepConfigFilePath = FileHelper.SaveResultFile(TestContext, "bicepconfig.json", bicepConfigFileContents);
                Directory.SetCurrentDirectory(Path.GetDirectoryName(bicepConfigFilePath)!);
                var bicepConfigUri = DocumentUri.FromFileSystemPath(bicepConfigFilePath);

                fileSystemDict[bicepConfigUri.ToUri()] = bicepConfigFileContents;

                client.Workspace.DidChangeWatchedFiles(new DidChangeWatchedFilesParams
                {
                    Changes = new Container<FileEvent>(new FileEvent
                    {
                        Type = FileChangeType.Created,
                        Uri = bicepConfigUri,
                    })
                });

                var diagsParams = await diagsListener.WaitNext();
                diagsParams.Uri.Should().Be(mainUri);
                diagsParams.Diagnostics.Should().SatisfyRespectively(
                    x =>
                    {
                        x.Message.Should().Be(@"Parameter ""storageAccountName"" is declared but never used.");
                        x.Severity.Should().Be(DiagnosticSeverity.Information);
                        x.Code?.String.Should().Be("https://aka.ms/bicep/linter/no-unused-params");
                        x.Range.Should().Be(new Range
                        {
                            Start = new Position(0, 6),
                            End = new Position(0, 24)
                        });
                    });
            }
        }

        [TestMethod]
        public async Task WithBicepConfigInParentDirectory_WhenNewBicepConfigFileIsAddedToCurrentDirectory_ShouldUseNewlyAddedConfigSettings()
        {
            var fileSystemDict = new Dictionary<Uri, string>();
            var diagsListener = new MultipleMessageListener<PublishDiagnosticsParams>();
            var serverOptions = new Server.CreationOptions(FileResolver: new InMemoryFileResolver(fileSystemDict));
            var client = await IntegrationTestHelper.StartServerWithClientConnectionAsync(
                TestContext,
                options =>
                {
                    options.OnPublishDiagnostics(diags => diagsListener.AddMessage(diags));
                },
                serverOptions);

            var mainUri = DocumentUri.FromFileSystemPath("/path/to/main.bicep");
            fileSystemDict[mainUri.ToUri()] = @"param storageAccountName string = 'test'";

            string bicepConfigFileContents = @"{
  ""analyzers"": {
    ""core"": {
      ""verbose"": false,
      ""enabled"": true,
      ""rules"": {
        ""no-unused-params"": {
          ""level"": ""info""
        }
      }
    }
  }
}";

            string bicepConfigFilePath = FileHelper.SaveResultFile(TestContext, "bicepconfig.json", bicepConfigFileContents);
            string? directoryContainingBicepConfigFile = Path.GetDirectoryName(bicepConfigFilePath);
            DirectoryInfo directoryInfo = Directory.CreateDirectory(Path.Combine(directoryContainingBicepConfigFile!, "BicepConfig"));
            string currentDirectory = Path.Combine(directoryInfo.FullName);
            Directory.SetCurrentDirectory(currentDirectory);
            var bicepConfigUri = DocumentUri.FromFileSystemPath(bicepConfigFilePath);

            fileSystemDict[bicepConfigUri.ToUri()] = bicepConfigFileContents;

            // open the main document and verify diagnostics
            {
                client.TextDocument.DidOpenTextDocument(TextDocumentParamHelper.CreateDidOpenDocumentParams(mainUri, fileSystemDict[mainUri.ToUri()], 1));

                var diagsParams = await diagsListener.WaitNext();
                diagsParams.Uri.Should().Be(mainUri);
                diagsParams.Diagnostics.Should().SatisfyRespectively(
                    x =>
                    {
                        x.Message.Should().Be(@"Parameter ""storageAccountName"" is declared but never used.");
                        x.Severity.Should().Be(DiagnosticSeverity.Information);
                        x.Code?.String.Should().Be("https://aka.ms/bicep/linter/no-unused-params");
                        x.Range.Should().Be(new Range
                        {
                            Start = new Position(0, 6),
                            End = new Position(0, 24)
                        });
                    });
            }

            // create new bicepconfig.json and verify diagnostics
            {
                bicepConfigFileContents = @"{
  ""analyzers"": {
    ""core"": {
      ""verbose"": false,
      ""enabled"": true,
      ""rules"": {
        ""no-unused-params"": {
          ""level"": ""warning""
        }
      }
    }
  }
}";
                bicepConfigFilePath = Path.Combine(currentDirectory, "bicepconfig.json");
                File.WriteAllText(bicepConfigFilePath, bicepConfigFileContents);
                var newBicepConfigUri = DocumentUri.FromFileSystemPath(bicepConfigFilePath);

                fileSystemDict[newBicepConfigUri.ToUri()] = bicepConfigFileContents;

                client.Workspace.DidChangeWatchedFiles(new DidChangeWatchedFilesParams
                {
                    Changes = new Container<FileEvent>(new FileEvent
                    {
                        Type = FileChangeType.Created,
                        Uri = newBicepConfigUri,
                    })
                });

                var diagsParams = await diagsListener.WaitNext();
                diagsParams.Uri.Should().Be(mainUri);
                diagsParams.Diagnostics.Should().SatisfyRespectively(
                    x =>
                    {
                        x.Message.Should().Be(@"Parameter ""storageAccountName"" is declared but never used.");
                        x.Severity.Should().Be(DiagnosticSeverity.Warning);
                        x.Code?.String.Should().Be("https://aka.ms/bicep/linter/no-unused-params");
                        x.Range.Should().Be(new Range
                        {
                            Start = new Position(0, 6),
                            End = new Position(0, 24)
                        });
                    });
            }
        }

        [TestMethod]
        public async Task WithBicepConfigInCurrentDirectory_WhenNewBicepConfigFileIsAddedToParentDirectory_ShouldUseOldConfigSettings()
        {
            var fileSystemDict = new Dictionary<Uri, string>();
            var diagsListener = new MultipleMessageListener<PublishDiagnosticsParams>();
            var serverOptions = new Server.CreationOptions(FileResolver: new InMemoryFileResolver(fileSystemDict));
            var client = await IntegrationTestHelper.StartServerWithClientConnectionAsync(
                TestContext,
                options =>
                {
                    options.OnPublishDiagnostics(diags => diagsListener.AddMessage(diags));
                },
                serverOptions);

            var mainUri = DocumentUri.FromFileSystemPath("/path/to/main.bicep");
            fileSystemDict[mainUri.ToUri()] = @"param storageAccountName string = 'test'";

            string bicepConfigFileContents = @"{
  ""analyzers"": {
    ""core"": {
      ""verbose"": false,
      ""enabled"": true,
      ""rules"": {
        ""no-unused-params"": {
          ""level"": ""info""
        }
      }
    }
  }
}";

            string bicepConfigFilePath = FileHelper.SaveResultFile(TestContext, "bicepconfig.json", bicepConfigFileContents);
            string currentDirectory = Path.GetDirectoryName(bicepConfigFilePath)!;
            Directory.SetCurrentDirectory(currentDirectory);
            var bicepConfigUri = DocumentUri.FromFileSystemPath(bicepConfigFilePath);

            fileSystemDict[bicepConfigUri.ToUri()] = bicepConfigFileContents;

            // open the main document and verify diagnostics
            {
                client.TextDocument.DidOpenTextDocument(TextDocumentParamHelper.CreateDidOpenDocumentParams(mainUri, fileSystemDict[mainUri.ToUri()], 1));

                var diagsParams = await diagsListener.WaitNext();
                diagsParams.Uri.Should().Be(mainUri);
                diagsParams.Diagnostics.Should().SatisfyRespectively(
                    x =>
                    {
                        x.Message.Should().Be(@"Parameter ""storageAccountName"" is declared but never used.");
                        x.Severity.Should().Be(DiagnosticSeverity.Information);
                        x.Code?.String.Should().Be("https://aka.ms/bicep/linter/no-unused-params");
                        x.Range.Should().Be(new Range
                        {
                            Start = new Position(0, 6),
                            End = new Position(0, 24)
                        });
                    });
            }

            // add bicepconfig.json to parent directory and verify diagnostics
            {
                DirectoryInfo? parentDirectory = Directory.GetParent(currentDirectory);
                bicepConfigFileContents = @"{
  ""analyzers"": {
    ""core"": {
      ""verbose"": false,
      ""enabled"": true,
      ""rules"": {
        ""no-unused-params"": {
          ""level"": ""warning""
        }
      }
    }
  }
}";
                string newBicepConfigFilePath = Path.Combine(parentDirectory!.FullName, "bicepconfig.json");
                File.WriteAllText(newBicepConfigFilePath, bicepConfigFileContents);
                var newBicepConfigUri = DocumentUri.FromFileSystemPath(newBicepConfigFilePath);

                fileSystemDict[newBicepConfigUri.ToUri()] = bicepConfigFileContents;

                client.Workspace.DidChangeWatchedFiles(new DidChangeWatchedFilesParams
                {
                    Changes = new Container<FileEvent>(new FileEvent
                    {
                        Type = FileChangeType.Created,
                        Uri = newBicepConfigUri,
                    })
                });

                var diagsParams = await diagsListener.WaitNext();
                diagsParams.Uri.Should().Be(mainUri);
                diagsParams.Diagnostics.Should().SatisfyRespectively(
                    x =>
                    {
                        x.Message.Should().Be(@"Parameter ""storageAccountName"" is declared but never used.");
                        x.Severity.Should().Be(DiagnosticSeverity.Information);
                        x.Code?.String.Should().Be("https://aka.ms/bicep/linter/no-unused-params");
                        x.Range.Should().Be(new Range
                        {
                            Start = new Position(0, 6),
                            End = new Position(0, 24)
                        });
                    });
            }
        }

        [TestMethod]
        public async Task InvalidBicepConfigFile_ShouldRefreshCompilation()
        {
            var fileSystemDict = new Dictionary<Uri, string>();
            var diagsListener = new MultipleMessageListener<PublishDiagnosticsParams>();
            var serverOptions = new Server.CreationOptions(FileResolver: new InMemoryFileResolver(fileSystemDict));
            var client = await IntegrationTestHelper.StartServerWithClientConnectionAsync(
                TestContext,
                options =>
                {
                    options.OnPublishDiagnostics(diags => diagsListener.AddMessage(diags));
                },
                serverOptions);

            var mainUri = DocumentUri.FromFileSystemPath("/path/to/main.bicep");
            fileSystemDict[mainUri.ToUri()] = @"param storageAccountName string = 'test'";

            string bicepConfigFileContents = @"{
  ""analyzers"": {
    ""core"": {
      ""verbose"": false,
      ""enabled"": true,
      ""rules"": {
        ""no-unused-params"": {
";

            string bicepConfigFilePath = FileHelper.SaveResultFile(TestContext, "bicepconfig.json", bicepConfigFileContents);
            Directory.SetCurrentDirectory(Path.GetDirectoryName(bicepConfigFilePath)!);
            var bicepConfigUri = DocumentUri.FromFileSystemPath(bicepConfigFilePath);

            fileSystemDict[bicepConfigUri.ToUri()] = bicepConfigFileContents;

            // open the main document and verify diagnostics
            {
                client.TextDocument.DidOpenTextDocument(TextDocumentParamHelper.CreateDidOpenDocumentParams(mainUri, fileSystemDict[mainUri.ToUri()], 1));

                var diagsParams = await diagsListener.WaitNext();
                diagsParams.Uri.Should().Be(mainUri);
                diagsParams.Diagnostics.Should().SatisfyRespectively(
                    x =>
                    {
                        x.Message.Should().Be(@"Could not load configuration file. Expected depth to be zero at the end of the JSON payload. There is an open JSON object or array that should be closed. LineNumber: 7 | BytePositionInLine: 0.");
                        x.Severity.Should().Be(DiagnosticSeverity.Error);
                        x.Range.Should().Be(new Range
                        {
                            Start = new Position(0, 0),
                            End = new Position(1, 0)
                        });
                    });
            }

            // update bicepconfig.json and verify diagnostics
            {
                client.TextDocument.DidChangeTextDocument(TextDocumentParamHelper.CreateDidChangeTextDocumentParams(bicepConfigUri, @"{
  ""analyzers"": {
    ""core"": {
      ""verbose"": false,
      ""enabled"": true,
      ""rules"": {
        ""no-unused-params"": {
          ""level"": ""warning""
        }
      }
    }
  }
}", 2));

                var diagsParams = await diagsListener.WaitNext();
                diagsParams.Uri.Should().Be(mainUri);
                diagsParams.Diagnostics.Should().SatisfyRespectively(
                    x =>
                    {
                        x.Message.Should().Be(@"Parameter ""storageAccountName"" is declared but never used.");
                        x.Severity.Should().Be(DiagnosticSeverity.Warning);
                        x.Code?.String.Should().Be("https://aka.ms/bicep/linter/no-unused-params");
                        x.Range.Should().Be(new Range
                        {
                            Start = new Position(0, 6),
                            End = new Position(0, 24)
                        });
                    });
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.SetCurrentDirectory(CurrentDirectory);
        }
    }
}
