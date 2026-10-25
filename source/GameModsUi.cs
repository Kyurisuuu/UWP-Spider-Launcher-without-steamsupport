using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.System.Profile;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace SpiderManInstaller
{
    public sealed partial class MainPage
    {
        bool modsBusy;
        void RefreshMods_Click(object sender, RoutedEventArgs e) => RefreshMods();
        void RefreshMods()
        {
            if (modsBusy) return;
            try
            {
                string root = GameStorage.OpenModsRoot();
                ModFolder.Text = AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Xbox"
                    ? @"From your PC: \\<Xbox IP>\DevelopmentFiles\SpiderManInstaller-Mods"
                    : root;
                var mods = AssetPreparation.ModOverlay.FindMods(root);
                ModList.Text = mods.Count == 0 ? "No mods found in the mods folder." : String.Join("\n", mods.Select(p => "• " + Path.GetFileName(p)));
                bool ready = GamePreparation.Ready(), applied = GamePreparation.ModsApplied();
                ApplyModsButton.IsEnabled = !busy && ready && mods.Count > 0;
                RemoveModsButton.IsEnabled = !busy && applied;
                ModStatus.Text = !ready ? "Prepare the game before applying mods."
                    : applied ? "Mods are applied. Apply again after changing the mods folder, or remove them to restore the prepared game."
                    : "No mods applied. Close the game before applying or removing mods.";
            }
            catch (Exception ex) { ModStatus.Text = "Mods unavailable: " + ex.Message; }
        }
        async void ApplyMods_Click(object sender, RoutedEventArgs e)
        {
            await RunModAction(() =>
            {
                var result = GamePreparation.ApplyMods(m => { var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => ModStatus.Text = m); });
                return "Applied " + result.Assets + " modded file(s) from " + result.Mods + " mod(s). Start the game to try them.";
            }, "Mods not applied: ");
        }
        async void RemoveMods_Click(object sender, RoutedEventArgs e)
        {
            await RunModAction(() => GamePreparation.RemoveMods() ? "Mods removed. The prepared game is restored." : "No mods were applied.", "Mods not removed: ");
        }
        async Task RunModAction(Func<string> action, string failure)
        {
            if (busy || modsBusy) return;
            busy = modsBusy = true;
            ApplyModsButton.IsEnabled = RemoveModsButton.IsEnabled = PlayButton.IsEnabled = PrepareButton.IsEnabled = false;
            string message;
            try { message = await Task.Run(action); }
            catch (Exception ex) { message = failure + ex.Message + " Close the game, then apply again or choose Remove mods to restore the prepared game."; }
            finally { busy = modsBusy = false; RefreshPreparation(); RefreshMods(); }
            ModStatus.Text = message;
        }
    }
}
