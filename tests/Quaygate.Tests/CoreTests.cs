using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quaygate.Tests
{
    [TestClass]
    public class CoreTests
    {
        string directory;
        Scenarios scenarios;
        [TestInitialize] public void SetUp()
        {
            directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TestResults",Guid.NewGuid().ToString("N"));
            scenarios=new Scenarios(directory);
        }
        [TestCleanup] public void TearDown() => Directory.Delete(directory,true);
        [TestMethod] public void LegacyJsonRoundTripAndBackup() => scenarios.Storage();
        [TestMethod] public void NestedImportAndLocalEdits() => scenarios.ImportAndMerge();
        [TestMethod] public void ImportedSiteDefaultsAndOverrides() => scenarios.ImportSiteInheritance();
        [TestMethod] public void SearchAndSiteOrder() => scenarios.SearchAndSites();
        [TestMethod] public void LaunchArgumentsAndFiles() => scenarios.Launch();
        [TestMethod] public void OnlySelectedEndpointIsProbedAndCached() => scenarios.SelectedEndpointProbe();
        [TestMethod] public void CredentialManagerLifecycle() => scenarios.CredentialLifecycle();
        [TestMethod] public void ThemeContrastRenderingAndSearch() => VisualScenario.Run(directory);
    }
}
