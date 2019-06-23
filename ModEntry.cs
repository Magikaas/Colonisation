using System;
using System.IO;

namespace Colonisation.NewJobs
{
    [ModLoader.ModManager]
    public static class ModEntry
    {
        public static string ModFolder;
        public static string ModGameDataDirectory;
        public static string LocalizationFolder;
        public static Version ModVersion = new Version(0, 0, 0, 1);
        public const string ModName = "Colonisation";
        public const string Naming = "Colonisation.Jobs.";

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterModsLoaded, Naming + "AfterModsLoaded")]
        [ModLoader.ModCallbackProvidesFor("Colonisation.Colonisation.Dependencies")]
        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAssemblyLoaded, Naming + "OnAssemblyLoaded")]
        public static void OnAssemblyLoaded(string path)
        {
            ModFolder = Path.GetDirectoryName(path).Replace("\\", "/");
            ModGameDataDirectory = Path.Combine(Path.GetDirectoryName(path), "gamedata/").Replace("\\", "/");
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterStartup, Naming + "AfterStartup")]
        public static void AfterStartup()
        {
            string versionURL = "https://raw.githubusercontent.com/Magikaas/Colonisation/master/Colonisation.md";

            PhentrixGames.NewColonyAPI.Helpers.Utilities.WriteLog("Colonisation", "Modname: " + ModName + ". ModFolder: " + ModFolder + ". Config: " + ModFolder + "/config. VersionURL: " + versionURL);
            
            PhentrixGames.NewColonyAPI.Managers.ModManager.RegisterMod(ModName, ModFolder, ModVersion, ModFolder + "/config");
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, Naming + "RegisterJobs")]
        [ModLoader.ModCallbackDependsOn("create_servermanager_trackers")]
        [ModLoader.ModCallbackDependsOn("pipliz.server.loadnpctypes")]
        [ModLoader.ModCallbackProvidesFor("create_savemanager")]
        public static void RegisterJobs()
        {
            Pipliz.Log.Write("Colonisation Test");

            Colonisation.ScoutJob.ScoutJobSettings scoutJobSettings = new Colonisation.ScoutJob.ScoutJobSettings("Scout", "colonisation.scoutjob", new InventoryItem("scoutrallypoint"));
            ServerManager.BlockEntityCallbacks.RegisterEntityManager(new Jobs.BlockJobManager<Colonisation.ScoutJob.ScoutJobInstance>(scoutJobSettings));
        }
    }
}