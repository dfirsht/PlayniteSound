﻿using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Media;
using Playnite.SDK.Events;
using System.Windows.Media.Animation;
using System.Diagnostics;
using Microsoft.Win32;
using System.Windows;
using System.IO.Compression;

namespace PlayniteSounds
{
    public class PlayniteSounds : Plugin
    {
        private static readonly string AppName = "Playnite Sounds";
        private static readonly IResourceProvider resources = new ResourceProvider();
        private static readonly ILogger logger = LogManager.GetLogger();
        public bool MusicNeedsReload { get; set; } = false;
        private PlayniteSoundsSettings Settings { get; set; }
        private string prevmusicfilename = "";
        private MediaPlayer musicplayer; 
        private readonly MediaTimeline timeLine;
        
        public static string pluginFolder;

        public override Guid Id { get; } = Guid.Parse("9c960604-b8bc-4407-a4e4-e291c6097c7d");

        private Dictionary<string, PlayerEntry> players = new Dictionary<string, PlayerEntry>();
        private bool closeaudiofilesnextplay = false;

        public PlayniteSounds(IPlayniteAPI api) : base(api)
        {
            try
            {
                Settings = new PlayniteSoundsSettings(this);

                pluginFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                Localization.SetPluginLanguage(pluginFolder, api.ApplicationSettings.Language);
                musicplayer = new MediaPlayer();
                timeLine = new MediaTimeline
                {
                    RepeatBehavior = RepeatBehavior.Forever
                };
            }
            catch (Exception E)
            {
                logger.Error(E, "PlayniteSounds");
                PlayniteApi.Dialogs.ShowErrorMessage(E.Message, AppName);
            }
        }

        public override void OnGameInstalled(Game game)
        {
            // Add code to be executed when game is finished installing.
            PlayFileName("GameInstalled.wav");
        }

        public override void OnGameStarted(Game game)
        {
            // Add code to be executed when game is started running.
            PlayFileName("GameStarted.wav", true);
        }

        public override void OnGameStarting(Game game)
        {
            // Add code to be executed when game is preparing to be started.
            PlayFileName("GameStarting.wav");
            PauseMusic();
        }

        public override void OnGameStopped(Game game, long elapsedSeconds)
        {
            // Add code to be executed when game is preparing to be started.
            PlayFileName("GameStopped.wav");
            ResumeMusic();
        }

        public override void OnGameUninstalled(Game game)
        {
            // Add code to be executed when game is uninstalled.
            PlayFileName("GameUninstalled.wav");
        }

        public override void OnApplicationStarted()
        {
            // Add code to be executed when Playnite is initialized.
            PlayFileName("ApplicationStarted.wav");
            SystemEvents.PowerModeChanged += new PowerModeChangedEventHandler(OnPowerMode_Changed);
        }

        //fix sounds not playing after system resume
        public void OnPowerMode_Changed(object sender, PowerModeChangedEventArgs e)
        {
            try
            { 
                if (e.Mode == PowerModes.Resume)
                {
                    closeaudiofilesnextplay = true;
                    MusicNeedsReload = true;
                    //Restart music:
                    ReplayMusic();
                }
            }
            catch (Exception E)
            {
                logger.Error(E, "OnPowerMode_Changed");
                PlayniteApi.Dialogs.ShowErrorMessage(E.Message, AppName);
            }
        }

        public void ResetMusicVolume()
        {
            musicplayer.Volume = (double)Settings.MusicVolume / 100;
        }

        public void ReplayMusic()
        {
            if (PlayniteApi.MainView.SelectedGames.Count() == 1)
            {
                foreach (Game game in PlayniteApi.MainView.SelectedGames)
                {
                    if (Settings.MusicType == 2)
                    {
                        PlayMusic(game.Name, game.Platform == null ? "No Platform" : game.Platform.ToString());
                    }
                    else
                    {
                        if (Settings.MusicType == 1)
                        {
                            PlayMusic("_music_", game.Platform == null ? "No Platform" : game.Platform.ToString());
                        }
                        else
                        {
                            PlayMusic("_music_", "");
                        }
                    }
                }
            }
        }

        public override void OnApplicationStopped()
        {
            // Add code to be executed when Playnite is shutting down.
            PlayFileName("ApplicationStopped.wav", true);
            CloseAudioFiles();
            CloseMusic();
            musicplayer = null;
        }

        public override void OnLibraryUpdated()
        {
            // Add code to be executed when library is updated.
            PlayFileName("LibraryUpdated.wav");
        }

        public override void OnGameSelected(GameSelectionEventArgs args)
        {
            PlayFileName("GameSelected.wav");
            if (args.NewValue.Count == 1) 
            {
                foreach(Game game in args.NewValue)
                {
                    if (Settings.MusicType == 2)
                    {
                        PlayMusic(game.Name, game.Platform == null ? "No Platform" : game.Platform.ToString());
                    }
                    else
                    {
                        if (Settings.MusicType == 1)
                        {
                            PlayMusic("_music_", game.Platform == null ? "No Platform" : game.Platform.ToString());
                        }
                        else
                        {
                            PlayMusic("_music_", "");
                        }
                    }
                    
                }
            }
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return Settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new PlayniteSoundsSettingsView(this);
        }

        public override List<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            List<GameMenuItem> MainMenuItems = new List<GameMenuItem>
            {
                new GameMenuItem {
                    MenuSection = "Playnite Sounds",
                    Icon = Path.Combine(pluginFolder, "icon.png"),
                    Description = resources.GetString("LOC_PLAYNITESOUNDS_ActionsShowMusicFilename"),
                    Action = (MainMenuItem) =>
                    {
                        ShowMusicFilename();
                    }
                }                
             };
            return MainMenuItems;
        }

        public override List<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            List<MainMenuItem> MainMenuItems = new List<MainMenuItem>
            {                
                new MainMenuItem {
                    MenuSection = "@Playnite Sounds",
                    Icon = Path.Combine(pluginFolder, "icon.png"),
                    Description = resources.GetString("LOC_PLAYNITESOUNDS_ActionsShowMusicFilename"),
                    Action = (MainMenuItem) =>
                    {
                        ShowMusicFilename();
                    }
                },
                new MainMenuItem {
                    MenuSection = "@Playnite Sounds",
                    Icon = Path.Combine(pluginFolder, "icon.png"),
                    Description = resources.GetString("LOC_PLAYNITESOUNDS_ActionsOpenMusicFolder"),
                    Action = (MainMenuItem) =>
                    {
                        OpenMusicFolder();
                    }
                },
                new MainMenuItem {
                    MenuSection = "@Playnite Sounds",
                    Icon = Path.Combine(pluginFolder, "icon.png"),
                    Description = resources.GetString("LOC_PLAYNITESOUNDS_ActionsOpenSoundsFolder"),
                    Action = (MainMenuItem) =>
                    {
                        OpenSoundsFolder();
                    }
                },
                new MainMenuItem {
                    MenuSection = "@Playnite Sounds",
                    Icon = Path.Combine(pluginFolder, "icon.png"),
                    Description = resources.GetString("LOC_PLAYNITESOUNDS_ActionsReloadAudioFiles"),
                    Action = (MainMenuItem) =>
                    {
                        ReloadAudioFiles();
                    }
                },
                new MainMenuItem {
                    MenuSection = "@Playnite Sounds",
                    Icon = Path.Combine(pluginFolder, "icon.png"),
                    Description = resources.GetString("LOC_PLAYNITESOUNDS_ActionsHelp"),
                    Action = (MainMenuItem) =>
                    {
                        HelpMenu();
                    }
                }
             };
            return MainMenuItems;
        }

        public void HelpMenu()
        {
            PlayniteApi.Dialogs.ShowMessage(resources.GetString("LOC_PLAYNITESOUNDS_MsgHelp1") + "\n\n" +
                resources.GetString("LOC_PLAYNITESOUNDS_MsgHelp2") + "\n\n" +
                resources.GetString("LOC_PLAYNITESOUNDS_MsgHelp3") + " " +
                resources.GetString("LOC_PLAYNITESOUNDS_MsgHelp4") + " " +
                resources.GetString("LOC_PLAYNITESOUNDS_MsgHelp5") + "\n\n" +
                resources.GetString("LOC_PLAYNITESOUNDS_MsgHelp6") + "\n\n" +
                "D_ApplicationStarted.wav - F_ApplicationStarted.wav\n" +
                "D_ApplicationStopped.wav - F_ApplicationStopped.wav\n" +
                "D_GameInstalled.wav - F_GameInstalled.wav\n" +
                "D_GameSelected.wav - F_GameSelected.wav\n" +
                "D_GameStarted.wav - F_GameStarted.wav\n" +
                "D_GameStarting.wav - F_GameStarting.wav\n" +
                "D_GameStopped.wav - F_GameStopped.wav\n" +
                "D_GameUninstalled.wav - F_GameUninstalled.wav\n" +
                "D_LibraryUpdated.wav - F_LibraryUpdated.wav\n\n" +
                resources.GetString("LOC_PLAYNITESOUNDS_MsgHelp7"), AppName);
        }

        public string GetMusicFilename(string gamename, string platform)
        {
            try
            { 
                string musisdir = Path.Combine(GetPluginUserDataPath(), "Music Files", platform);
                Directory.CreateDirectory(musisdir);
                string invalidChars = new string(Path.GetInvalidFileNameChars());
                Regex r = new Regex(string.Format("[{0}]", Regex.Escape(invalidChars)));
                string sanitizedgamename = r.Replace(gamename, "") + ".mp3";
                return Path.Combine(musisdir, sanitizedgamename);
            }
            catch (Exception E)
            {
                logger.Error(E, "GetMusicFilename");
                PlayniteApi.Dialogs.ShowErrorMessage(E.Message, AppName);
                return "";
            }
        }

        public void ShowMusicFilename()
        {
            if (PlayniteApi.MainView.SelectedGames.Count() == 1)
            {
                foreach (Game game in PlayniteApi.MainView.SelectedGames)
                {
                    string MusicFileName;
                    if (Settings.MusicType == 2)
                    {
                        MusicFileName = GetMusicFilename(game.Name, game.Platform == null ? "No Platform" : game.Platform.ToString());
                    }
                    else
                    {
                        if (Settings.MusicType == 1)
                        {
                            MusicFileName = GetMusicFilename("_music_", game.Platform == null ? "No Platform" : game.Platform.ToString());
                        }
                        else
                        {
                            MusicFileName = GetMusicFilename("_music_", "");
                        }
                    }
                    PlayniteApi.Dialogs.ShowMessage(MusicFileName, "Playnite Sounds");
                }
            }
            else
            {
                PlayniteApi.Dialogs.ShowMessage(resources.GetString("LOC_PLAYNITESOUNDS_MsgSelectSingleGame"), AppName);
            }
        }

        public void PlayFileName(string FileName, bool UseSoundPlayer = false)
        {
            try
            { 
                InitialCopyAudioFiles();

                if (closeaudiofilesnextplay)
                {
                    CloseAudioFiles();
                    closeaudiofilesnextplay = false;
                }

                bool DesktopMode = PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop;
                bool DoPlay = (DesktopMode && ((Settings.SoundWhere == 1) || (Settings.SoundWhere == 3))) ||
                    (!DesktopMode && ((Settings.SoundWhere == 2) || (Settings.SoundWhere == 3)));

                if (DoPlay)
                {
                    PlayerEntry Entry;
                    if (players.ContainsKey(FileName))
                    {
                        Entry = players[FileName];
                    }
                    else
                    {
                        string Prefix = PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop ? "D_" : "F_";

                        string FullFileName = Path.Combine(GetPluginUserDataPath(), "Sound Files", Prefix + FileName);

                        //MediaPlayer can play multiple sounds together from mulitple instances SoundPlayer can not
                        if (UseSoundPlayer)
                        {
                            Entry = new PlayerEntry(File.Exists(FullFileName), null, new SoundPlayer(), 0);
                        }
                        else
                        {
                            Entry = new PlayerEntry(File.Exists(FullFileName), new MediaPlayer(), null, 1);
                        }

                        if (Entry.FileExists)
                        {
                            if (Entry.TypePlayer == 1)
                            {
                                Entry.MediaPlayer.Open(new Uri(FullFileName));
                            }
                            else
                            {
                                Entry.SoundPlayer.SoundLocation = FullFileName;
                                Entry.SoundPlayer.Load();
                            }
                        }
                        players[FileName] = Entry;
                    }

                    if (Entry.FileExists)
                    {
                        if (Entry.TypePlayer == 1)
                        {
                            Entry.MediaPlayer.Stop();
                            Entry.MediaPlayer.Play();
                        }
                        else
                        {
                            Entry.SoundPlayer.Stop();
                            Entry.SoundPlayer.PlaySync();
                        }
                    }
                }
            }
            catch (Exception E)
            {
                logger.Error(E, "PlayFileName");
                PlayniteApi.Dialogs.ShowErrorMessage(E.Message, AppName);
            }
        }


        public void CloseAudioFiles()
        {
            try
            {
                foreach (string keyname in players.Keys)
                {
                    PlayerEntry Entry = players[keyname];
                    if (Entry.TypePlayer == 1)
                    {
                        Entry.MediaPlayer.Stop();
                        Entry.MediaPlayer.Close();
                        Entry.MediaPlayer = null;
                    }
                    else
                    {
                        Entry.SoundPlayer.Stop();
                        Entry.SoundPlayer = null;
                    }
                }
                players.Clear();
            }
            catch (Exception E)
            {
                logger.Error(E, "CloseAudioFiles");
                PlayniteApi.Dialogs.ShowErrorMessage(E.Message, AppName);
            }
        }

        public void ReloadAudioFiles()
        {
            CloseAudioFiles();
            PlayniteApi.Dialogs.ShowMessage(resources.GetString("LOC_PLAYNITESOUNDS_MsgAudioFilesReloaded"), AppName);
        }

        public void InitialCopyAudioFiles()
        {
            try
            { 
                string SoundFilesInstallPath = Path.Combine(pluginFolder, "Sound Files");
                string SoundFilesDataPath = Path.Combine(GetPluginUserDataPath(), "Sound Files");

                if (!Directory.Exists(SoundFilesDataPath))
                {
                    if (Directory.Exists(SoundFilesInstallPath))
                    {
                        CloseAudioFiles();

                        Directory.CreateDirectory(SoundFilesDataPath);
                        string[] files = Directory.GetFiles(SoundFilesInstallPath);
                        foreach (string file in files)
                        {                        
                            string DestPath = Path.Combine(SoundFilesDataPath, Path.GetFileName(file));
                            File.Copy(file, DestPath, true);
                        }
                    }
                }
            }
            catch (Exception E)
            {
                logger.Error(E, "InitialCopyAudioFiles");
                PlayniteApi.Dialogs.ShowErrorMessage(E.Message, AppName);
            }
        }

        public void ResumeMusic()
        {
            try
            { 
                if (musicplayer.Clock != null)
                {
                    musicplayer.Clock.Controller.Resume();
                }
            }
            catch (Exception E)
            {
                logger.Error(E, "ResumeMusic");
                PlayniteApi.Dialogs.ShowErrorMessage(E.Message, AppName);
            }
        }

        public void PauseMusic()
        {
            try
            { 
                if (musicplayer.Clock != null)
                {
                    musicplayer.Clock.Controller.Pause();
                }
            }
            catch (Exception E)
            {
                logger.Error(E, "PauseMusic");
                PlayniteApi.Dialogs.ShowErrorMessage(E.Message, AppName);
            }
        }

        public void  CloseMusic()
        {
            try
            {
                if (musicplayer.Clock != null)
                {
                    musicplayer.Clock.Controller.Stop();
                    musicplayer.Clock = null;
                    musicplayer.Close();
                }
            }
            catch (Exception E)
            {
                logger.Error(E, "CloseMusic");
                PlayniteApi.Dialogs.ShowErrorMessage(E.Message, AppName);
            }
}

        public void PlayMusic(string gamename, string platform)
        {
            try
            { 
                bool DesktopMode = PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop;
                bool DoPlay = (DesktopMode && ((Settings.MusicWhere == 1) || (Settings.MusicWhere == 3))) ||
                    (!DesktopMode && ((Settings.MusicWhere == 2) || (Settings.MusicWhere == 3)));

                if (DoPlay)
                {
                    string MusicFileName = GetMusicFilename(gamename, platform);
                    if (MusicNeedsReload || (MusicFileName != prevmusicfilename))
                    {
                        CloseMusic();
                        MusicNeedsReload = false;
                        prevmusicfilename = "";
                        if (File.Exists(MusicFileName))
                        {
                            prevmusicfilename = MusicFileName;
                            timeLine.Source = new Uri(MusicFileName);
                            musicplayer.Volume = (double)Settings.MusicVolume / 100;
                            musicplayer.Clock = timeLine.CreateClock();
                            musicplayer.Clock.Controller.Begin();
                            
                        }
                    }
                }
                else 
                { 
                    CloseMusic();
                }
            }
            catch (Exception E)
            {
                logger.Error(E, "PlayMusic");
                PlayniteApi.Dialogs.ShowErrorMessage(E.Message, AppName);
            }
        }

        public void OpenSoundsFolder()
        {
            try
            { 
                //need to release them otherwise explorer can't overwrite files even though you can delete them
                CloseAudioFiles();
                string SoundFilesDataPath = Path.Combine(GetPluginUserDataPath(), "Sound Files");
                // just in case user deleted it
                Directory.CreateDirectory(SoundFilesDataPath);
                Process.Start(SoundFilesDataPath);
            }
            catch (Exception E)
            {
                logger.Error(E, "OpenSoundsFolder");
                PlayniteApi.Dialogs.ShowErrorMessage(E.Message, AppName);
            }
        }

        public void OpenMusicFolder()
        {
            try
            {
                //need to release them otherwise explorer can't overwrite files even though you can delete them
                CloseMusic();
                string SoundFilesDataPath = Path.Combine(GetPluginUserDataPath(), "Music Files");
                //just in case user deleted it
                Directory.CreateDirectory(SoundFilesDataPath);
                Process.Start(SoundFilesDataPath);
                MusicNeedsReload = true;
            }
            catch (Exception E)
            {
                logger.Error(E, "OpenMusicFolder");
                PlayniteApi.Dialogs.ShowErrorMessage(E.Message, AppName);
            }
        }

        public void SaveSounds()
        {
            Window windowExtension = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = false,
                ShowCloseButton = true
            });

            windowExtension.ShowInTaskbar = false;
            windowExtension.ResizeMode = ResizeMode.NoResize;
            windowExtension.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
            windowExtension.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            StackPanel stackPanel = new StackPanel();
            stackPanel.Orientation = Orientation.Horizontal;

            TextBox saveNameBox = new TextBox();
            saveNameBox.Margin = new Thickness(5, 5, 10, 5);
            saveNameBox.Width = 200;
            stackPanel.Children.Add(saveNameBox);

            Button saveNameButton = new Button();
            saveNameButton.Margin = new Thickness(0, 5, 5, 5);
            saveNameButton.SetResourceReference(Button.ContentProperty, "LOC_PLAYNITESOUNDS_ManagerSave");
            saveNameButton.IsEnabled = false;
            saveNameButton.IsDefault = true;
            stackPanel.Children.Add(saveNameButton);

            saveNameBox.KeyUp += (object sender, System.Windows.Input.KeyEventArgs e) =>
            {
                // Only allow saving if filename is larger than 3 characters
                saveNameButton.IsEnabled = saveNameBox.Text.Trim().Length > 3;
            };

            saveNameButton.Click += (object sender, RoutedEventArgs e) =>
            {
                // Create ZIP file in sound manager folder
                try
                {
                    string soundPackName = saveNameBox.Text;
                    string SoundFilesDataPath = Path.Combine(GetPluginUserDataPath(), "Sound Files");
                    //just in case user deleted it
                    Directory.CreateDirectory(SoundFilesDataPath);
                    string SoundManagerFilesDataPath = Path.Combine(GetPluginUserDataPath(), "Sound Manager");
                    //just in case user deleted it
                    Directory.CreateDirectory(SoundManagerFilesDataPath);
                    ZipFile.CreateFromDirectory(SoundFilesDataPath, SoundManagerFilesDataPath + "\\" + soundPackName + ".zip");
                    PlayniteApi.Dialogs.ShowMessage(Application.Current.FindResource("LOC_PLAYNITESOUNDS_ManagerSaveConfirm").ToString() + " " + soundPackName);
                    windowExtension.Close();
                }
                catch (Exception E)
                {
                    logger.Error(E, "SaveSounds");
                    PlayniteApi.Dialogs.ShowErrorMessage(E.Message, AppName);
                }
            };

            windowExtension.Content = stackPanel;
            windowExtension.SizeToContent = SizeToContent.WidthAndHeight;
            // Workaround for WPF bug which causes black sections to be displayed in the window
            windowExtension.ContentRendered += (s, e) => windowExtension.InvalidateMeasure();
            windowExtension.Loaded += (s, e) => saveNameBox.Focus();
            windowExtension.ShowDialog();
        }

        public void LoadSounds()
        {
            try
            {
                string SoundManagerFilesDataPath = Path.Combine(GetPluginUserDataPath(), "Sound Manager");
                //just in case user deleted it
                Directory.CreateDirectory(SoundManagerFilesDataPath);

                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Filter = "ZIP archive|*.zip";
                dialog.InitialDirectory = SoundManagerFilesDataPath;
                bool? result = dialog.ShowDialog(PlayniteApi.Dialogs.GetCurrentAppWindow());
                if (result == true)
                {
                    CloseAudioFiles();
                    string targetPath = dialog.FileName;
                    string SoundFilesDataPath = Path.Combine(GetPluginUserDataPath(), "Sound Files");
                    //just in case user deleted it
                    Directory.CreateDirectory(SoundFilesDataPath);
                    // Have to extract each file one at a time to enabled overwrites
                    using (ZipArchive archive = ZipFile.OpenRead(targetPath))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            // If it's a directory, it doesn't have a "Name".
                            if (!String.IsNullOrEmpty(entry.Name))
                            {
                                string entryDestination = Path.Combine(SoundFilesDataPath, entry.FullName);
                                entry.ExtractToFile(entryDestination, true);
                            }
                        }
                    }
                    PlayniteApi.Dialogs.ShowMessage(Application.Current.FindResource("LOC_PLAYNITESOUNDS_ManagerLoadConfirm").ToString() + " " + Path.GetFileNameWithoutExtension(targetPath));
                }
            }
            catch (Exception E)
            {
                logger.Error(E, "LoadSounds");
                PlayniteApi.Dialogs.ShowErrorMessage(E.Message, AppName);
            }


        }

        public void ImportSounds()
        {
            List<string> targetPaths = PlayniteApi.Dialogs.SelectFiles("ZIP archive|*.zip");

            if (targetPaths.HasNonEmptyItems())
            {
                try
                {
                    string SoundManagerFilesDataPath = Path.Combine(GetPluginUserDataPath(), "Sound Manager");
                    //just in case user deleted it
                    Directory.CreateDirectory(SoundManagerFilesDataPath);
                    foreach (string targetPath in targetPaths)
                    {
                        File.Copy(targetPath, SoundManagerFilesDataPath + "\\" + Path.GetFileName(targetPath));
                    }
                }
                catch (Exception E)
                {
                    logger.Error(E, "ImportSounds");
                    PlayniteApi.Dialogs.ShowErrorMessage(E.Message, AppName);
                }
            }
        }

        public void RemoveSounds()
        {
            try
            {
                string SoundManagerFilesDataPath = Path.Combine(GetPluginUserDataPath(), "Sound Manager");
                //just in case user deleted it
                Directory.CreateDirectory(SoundManagerFilesDataPath);

                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Filter = "ZIP archive|*.zip";
                dialog.InitialDirectory = SoundManagerFilesDataPath;
                bool? result = dialog.ShowDialog(PlayniteApi.Dialogs.GetCurrentAppWindow());
                if (result == true)
                {
                    string targetPath = dialog.FileName;
                    File.Delete(targetPath);
                    PlayniteApi.Dialogs.ShowMessage(Application.Current.FindResource("LOC_PLAYNITESOUNDS_ManagerDeleteConfirm").ToString() + " " + Path.GetFileNameWithoutExtension(targetPath));
                }
            }
            catch (Exception E)
            {
                logger.Error(E, "RemoveSounds");
                PlayniteApi.Dialogs.ShowErrorMessage(E.Message, AppName);
            }
        }

        public void OpenSoundManagerFolder()
        {
            try
            {
                string SoundManagerFilesDataPath = Path.Combine(GetPluginUserDataPath(), "Sound Manager");
                //just in case user deleted it
                Directory.CreateDirectory(SoundManagerFilesDataPath);
                Process.Start(SoundManagerFilesDataPath);
            }
            catch (Exception E)
            {
                logger.Error(E, "OpenSoundManagerFolder");
                PlayniteApi.Dialogs.ShowErrorMessage(E.Message, AppName);
            }
        }
    }
}