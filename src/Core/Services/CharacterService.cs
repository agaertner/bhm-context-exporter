using Blish_HUD;
using Blish_HUD.Extended;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Gw2Sharp.WebApi.V2.Models;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Blish_HUD.GameService;
namespace Nekres.Stream_Out.Core.Services {
    internal class CharacterService : ExportService
    {
        private static Gw2ApiManager Gw2ApiManager => StreamOutModule.Instance?.Gw2ApiManager;
        private DirectoriesManager DirectoriesManager => StreamOutModule.Instance?.DirectoriesManager;
        private ContentsManager ContentsManager => StreamOutModule.Instance?.ContentsManager;
        private SettingEntry<int> SessionDeathsWvW => StreamOutModule.Instance?.SessionDeathsWvW;
        private SettingEntry<int> TotalDeathsAtResetWvW => StreamOutModule.Instance?.TotalDeathsAtResetWvW;
        private SettingEntry<int> SessionDeathsDaily => StreamOutModule.Instance?.SessionDeathsDaily;
        private SettingEntry<int> TotalDeathsAtResetDaily => StreamOutModule.Instance?.TotalDeathsAtResetDaily;
        private StreamOutModule.UnicodeSigning UnicodeSigning => StreamOutModule.Instance?.AddUnicodeSymbols.Value ?? StreamOutModule.UnicodeSigning.Suffixed;

        private const string CHARACTER_NAME  = "character_name.txt";
        private const string PROFESSION_ICON = "profession_icon.png";
        private const string PROFESSION_NAME = "profession_name.txt";
        private const string COMMANDER_ICON  = "commander_icon.png";
        private const string DEATHS_WEEK     = "deaths_week.txt";
        private const string DEATHS_DAY      = "deaths_day.txt";
        private const string COMBAT_ICON     = "combat_icon.png";
        private const string COMBAT_TEXT     = "combat.txt";
        private const string SKULL           = "\u2620"; // ☠
        private const string SWORDS          = "\u2694"; // ⚔

        private Bitmap _commanderIcon;
        private Bitmap _catmanderIcon;
        private Bitmap _battleIcon;

        private SettingEntry<bool> UseCatmanderTag => StreamOutModule.Instance.UseCatmanderTag;

        public CharacterService()
        {
            Gw2Mumble.PlayerCharacter.NameChanged           += OnNameChanged;
            Gw2Mumble.PlayerCharacter.SpecializationChanged += OnSpecializationChanged;
            Gw2Mumble.PlayerCharacter.IsCommanderChanged    += OnIsCommanderChanged;
            Gw2Mumble.PlayerCharacter.IsInCombatChanged     += OnIsInCombatChanged;
            UseCatmanderTag.SettingChanged                  += OnUseCatmanderTagSettingChanged;
            OnNameChanged(null, new ValueEventArgs<string>(Gw2Mumble.PlayerCharacter.Name));
            OnSpecializationChanged(null, new ValueEventArgs<int>(Gw2Mumble.PlayerCharacter.Specialization));
        }

        public override async Task Initialize()
        {
            await FileUtil.WriteAllTextAsync($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{DEATHS_WEEK}", $"0{SKULL}", false);
            await FileUtil.WriteAllTextAsync($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{DEATHS_DAY}", $"0{SKULL}", false);

            using var catmanderIconStream = ContentsManager.GetFileStream("catmander_tag_white.png");
            _catmanderIcon = new Bitmap(catmanderIconStream);

            using var commanderIconStream = ContentsManager.GetFileStream("commander_tag_white.png");
            _commanderIcon = new Bitmap(commanderIconStream);

            using var battleIconStream = ContentsManager.GetFileStream("240678.png");
            _battleIcon = new Bitmap(battleIconStream);
        }

        private async void OnIsInCombatChanged(object o, ValueEventArgs<bool> e) {
            await FileUtil.WriteAllTextAsync($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{COMBAT_TEXT}", e.Value ? SWORDS : string.Empty);
            if (!e.Value) {
                await TextureUtil.ClearImage($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{COMBAT_ICON}");
                return;
            }
            await _battleIcon.SaveOnNetworkShare($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{COMBAT_ICON}", ImageFormat.Png);
        }

        private async void OnNameChanged(object o, ValueEventArgs<string> e)
        {
            await FileUtil.WriteAllTextAsync($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{CHARACTER_NAME}", e.Value ?? string.Empty);
        }

        private async void OnSpecializationChanged(object o, ValueEventArgs<int> e)
        {
            if (e.Value <= 0)
            {
                await TextureUtil.ClearImage($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{PROFESSION_ICON}");
                return;
            }

            var specialization = await TaskUtil.RetryAsync(() => Gw2ApiManager.Gw2ApiClient.V2.Specializations.GetAsync(e.Value)).Unwrap();

            if (specialization == null) {
                return;
            }

            Gw2Sharp.WebApi.RenderUrl? icon;
            string name;

            if (specialization.Elite) {

                icon = specialization.ProfessionIconBig;
                name = specialization.Name;

            } else {

                var profession = await TaskUtil.RetryAsync(() => Gw2ApiManager.Gw2ApiClient.V2.Professions.GetAsync(Gw2Mumble.PlayerCharacter.Profession)).Unwrap();

                if (profession == null) {
                    return;
                }

                icon = profession.IconBig;
                name = profession.Name;

            }

            await FileUtil.WriteAllTextAsync($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{PROFESSION_NAME}", name ?? string.Empty);
            await TextureUtil.SaveToImage(icon, $"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{PROFESSION_ICON}");
        }

        private async void OnIsCommanderChanged(object o, ValueEventArgs<bool> e)
        {
            if (!e.Value)
            {
                await TextureUtil.ClearImage($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{COMMANDER_ICON}");
                return;
            }
            await SaveCommanderIcon(UseCatmanderTag.Value);
        }

        private async Task SaveCommanderIcon(bool useCatmanderIcon) {
            var icon = useCatmanderIcon ? _catmanderIcon : _commanderIcon;
            await icon.SaveOnNetworkShare($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{COMMANDER_ICON}", ImageFormat.Png);
        }

        private async void OnUseCatmanderTagSettingChanged(object o, ValueChangedEventArgs<bool> e)
        {
            if (!Gw2Mumble.PlayerCharacter.IsCommander) {
                return;
            }
            await SaveCommanderIcon(e.NewValue);
        }

        public static async Task<int> RequestTotalDeaths()
        {
            if (!Gw2ApiManager.HasPermissions(new[] { TokenPermission.Account, TokenPermission.Characters })) {
                return -1;
            }

            return await Gw2ApiManager.Gw2ApiClient.V2.Characters.AllAsync().ContinueWith(task => task.IsFaulted ? -1 : task.Result.Sum(x => x.Deaths));
        }

        protected override async Task ResetDaily()
        {
            SessionDeathsDaily.Value = 0;
            TotalDeathsAtResetDaily.Value = await RequestTotalDeaths();
        }

        protected override async Task Update()
        {
            var prefixDeaths = UnicodeSigning == StreamOutModule.UnicodeSigning.Prefixed ? SKULL : string.Empty;
            var suffixDeaths = UnicodeSigning == StreamOutModule.UnicodeSigning.Suffixed ? SKULL : string.Empty;

            // Deaths
            var totalDeaths = await RequestTotalDeaths();
            if (totalDeaths >= 0)
            {
                SessionDeathsDaily.Value = totalDeaths - TotalDeathsAtResetDaily.Value;
                SessionDeathsWvW.Value = totalDeaths - TotalDeathsAtResetWvW.Value;
                await FileUtil.WriteAllTextAsync($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{DEATHS_WEEK}", $"{prefixDeaths}{SessionDeathsWvW.Value}{suffixDeaths}");
                await FileUtil.WriteAllTextAsync($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{DEATHS_DAY}", $"{prefixDeaths}{SessionDeathsDaily.Value}{suffixDeaths}");
            }
        }

        public override async Task Clear()
        {
            var dir = DirectoriesManager.GetFullDirectoryPath("stream_out");
            await FileUtil.DeleteAsync(Path.Combine(dir, DEATHS_DAY));
            await FileUtil.DeleteAsync(Path.Combine(dir, DEATHS_WEEK));
            await FileUtil.DeleteAsync(Path.Combine(dir, CHARACTER_NAME));
            await FileUtil.DeleteAsync(Path.Combine(dir, PROFESSION_ICON));
            await FileUtil.DeleteAsync(Path.Combine(dir, COMMANDER_ICON));
        }

        public override void Dispose()
        {
            _commanderIcon?.Dispose();
            _catmanderIcon?.Dispose();
            _battleIcon?.Dispose();
            Gw2Mumble.PlayerCharacter.NameChanged           -= OnNameChanged;
            Gw2Mumble.PlayerCharacter.SpecializationChanged -= OnSpecializationChanged;
            UseCatmanderTag.SettingChanged                  -= OnUseCatmanderTagSettingChanged;
            Gw2Mumble.PlayerCharacter.IsCommanderChanged    -= OnIsCommanderChanged;
            Gw2Mumble.PlayerCharacter.IsInCombatChanged     -= OnIsInCombatChanged;
        }
    }
}
