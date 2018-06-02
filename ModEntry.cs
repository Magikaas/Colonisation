using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using BlockTypes.Builtin;
using ColonyTech.Classes;
using ColonyTech.BlockNPCs;
using ColonyTech.Managers;
using PhentrixGames.NewColonyAPI.Helpers;
using PhentrixGames.NewColonyAPI;
using Pipliz.Mods.APIProvider.Jobs;

namespace ColonyTech.Jobs
{
    [ModLoader.ModManager]
    public static class ModEntry
    {
        public static string ModFolder;
        public static string ModGameDataDirectory;
        public static string LocalizationFolder;
        public static Version ModVersion = new Version(0, 0, 0, 1);
        public const string ModName = "ColonyTech";
        public const string Naming = "ColonyTech.Jobs.";

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAssemblyLoaded, Naming + "OnAssemblyLoaded")]
        public static void OnAssemblyLoaded(string path)
        {
            ModFolder = Path.GetDirectoryName(path);
            ModGameDataDirectory = Path.Combine(Path.GetDirectoryName(path), "gamedata/").Replace("\\", "/");

            LocalizationFolder = Path.Combine(ModGameDataDirectory, "localization/").Replace("\\", "/");
            Utilities.CreateLogs("ColonyTech");
            PhentrixGames.NewColonyAPI.Managers.ConfigManager.registerConfig("Magikaas/ColonyTech", "config");
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterStartup, Naming + "AfterStartup")]
        public static void AfterStartup()
        {
            string versionURL = "https://raw.githubusercontent.com/Magikaas/ColonyTech/master/ColonyTech.md";
            string version1 = new WebClient().DownloadString(versionURL);
            
            PhentrixGames.NewColonyAPI.Managers.ModManager.RegisterMod(ModName, ModFolder, ModVersion, ModFolder + "/config", versionURL);
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterItemTypesDefined, Naming + "RegisterJobs")]
        [ModLoader.ModCallbackProvidesFor("pipliz.apiprovider.jobs.resolvetypes")]
        public static void RegisterJobs()
        {
            Pipliz.Log.Write("ColonyTech Test");
            BlockJobManagerTracker.Register<OreProcessorJob>(OreProcessorJob.JOB_STATION);
            BlockJobManagerTracker.Register<ScoutJob>(ScoutJob.JOB_STATION);
        }
    }
}