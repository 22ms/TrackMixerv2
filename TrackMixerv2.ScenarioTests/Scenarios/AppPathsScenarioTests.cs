using TrackMixerv2;

namespace TrackMixerv2.ScenarioTests.Scenarios;

[Collection(AppStateCollection.Name)]
public sealed class AppPathsScenarioTests : IDisposable
{
  private readonly string? _previousUiTestFlag;
  private readonly string? _previousRootFolders;

  public AppPathsScenarioTests()
  {
    _previousUiTestFlag = Environment.GetEnvironmentVariable(UiTestBootstrap.EnabledEnvVar);
    _previousRootFolders = Environment.GetEnvironmentVariable(AppPaths.RootFoldersEnvVar);
  }

  [Fact]
  public void Data_directory_uses_isolated_storage_when_ui_test_enabled()
  {
    string? previousSettingsPath = Environment.GetEnvironmentVariable(LocalSettingsStore.JsonPathEnvVar);
    string? previousMetadataPath = Environment.GetEnvironmentVariable(AppState.TrackMetadataJsonEnvVar);
    Environment.SetEnvironmentVariable(UiTestBootstrap.EnabledEnvVar, "1");
    Environment.SetEnvironmentVariable(LocalSettingsStore.JsonPathEnvVar, null);
    Environment.SetEnvironmentVariable(AppState.TrackMetadataJsonEnvVar, null);
    UiTestBootstrap.ResetIsolatedStorageForTests();
    LocalSettingsStore.ResetCache();

    try
    {
      string dataDirectory = AppPaths.DataDirectory;
      Assert.Contains("TrackMixerUITests", dataDirectory);
      Assert.StartsWith(dataDirectory, LocalSettingsStore.JsonPath, StringComparison.OrdinalIgnoreCase);
    }
    finally
    {
      Environment.SetEnvironmentVariable(UiTestBootstrap.EnabledEnvVar, _previousUiTestFlag);
      Environment.SetEnvironmentVariable(LocalSettingsStore.JsonPathEnvVar, previousSettingsPath);
      Environment.SetEnvironmentVariable(AppState.TrackMetadataJsonEnvVar, previousMetadataPath);
      UiTestBootstrap.ResetIsolatedStorageForTests();
      LocalSettingsStore.ResetCache();
    }
  }

  [Fact]
  public void Scratch_and_temp_registry_stay_under_isolated_storage_in_ui_test_mode()
  {
    Environment.SetEnvironmentVariable(UiTestBootstrap.EnabledEnvVar, "1");
    UiTestBootstrap.ResetIsolatedStorageForTests();

    try
    {
      string isolated = UiTestBootstrap.GetIsolatedStorageDirectory();
      Assert.StartsWith(isolated, AppPaths.ScratchDirectory, StringComparison.OrdinalIgnoreCase);
      Assert.StartsWith(isolated, AppPaths.TempFilesRecordPath, StringComparison.OrdinalIgnoreCase);
      Assert.StartsWith(isolated, AppPaths.UiTestCrashLogPath, StringComparison.OrdinalIgnoreCase);
    }
    finally
    {
      Environment.SetEnvironmentVariable(UiTestBootstrap.EnabledEnvVar, _previousUiTestFlag);
      UiTestBootstrap.ResetIsolatedStorageForTests();
    }
  }

  [Fact]
  public void ResolveRootFoldersFromEnvironment_prefers_single_root_folder_override()
  {
    Environment.SetEnvironmentVariable(UiTestBootstrap.RootFolderEnvVar, @"D:\Clips");
    Environment.SetEnvironmentVariable(AppPaths.RootFoldersEnvVar, @"D:\Other");

    try
    {
      var folders = UiTestBootstrap.ResolveRootFoldersFromEnvironment();
      Assert.Single(folders);
      Assert.Equal(@"D:\Clips", folders[0]);
    }
    finally
    {
      Environment.SetEnvironmentVariable(UiTestBootstrap.RootFolderEnvVar, null);
      Environment.SetEnvironmentVariable(AppPaths.RootFoldersEnvVar, _previousRootFolders);
    }
  }

  public void Dispose()
  {
    Environment.SetEnvironmentVariable(UiTestBootstrap.EnabledEnvVar, _previousUiTestFlag);
    Environment.SetEnvironmentVariable(AppPaths.RootFoldersEnvVar, _previousRootFolders);
    Environment.SetEnvironmentVariable(UiTestBootstrap.RootFolderEnvVar, null);
    UiTestBootstrap.ResetIsolatedStorageForTests();
    LocalSettingsStore.ResetCache();
  }
}
