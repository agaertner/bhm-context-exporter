using Blish_HUD.Extended;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Gw2Sharp.WebApi.V2.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Nekres.Stream_Out.Core.Services {
    internal class WvwService : ExportService
    {
        private Gw2ApiManager                  Gw2ApiManager      => StreamOutModule.Instance?.Gw2ApiManager;
        private DirectoriesManager             DirectoriesManager => StreamOutModule.Instance?.DirectoriesManager;
        private StreamOutModule.UnicodeSigning UnicodeSigning     => StreamOutModule.Instance?.AddUnicodeSymbols.Value ?? StreamOutModule.UnicodeSigning.Suffixed;

        private SettingEntry<DateTime> _nextResetTimeWvW;
        private SettingEntry<DateTime> _lastResetTimeWvW;
        private SettingEntry<int>      _killsAtResetDaily;
        private SettingEntry<int>      _killsAtResetMatch;

        private const string WVW_KILLS_WEEK  = "wvw_kills_week.txt";
        private const string WVW_KILLS_DAY   = "wvw_kills_day.txt";
        private const string WVW_KILLS_TOTAL = "wvw_kills_total.txt";
        private const string WVW_RANK        = "wvw_rank.txt";

        private const string SWORDS = "\u2694"; // ⚔

        public WvwService(SettingCollection settings) : base(settings)
        {
            _nextResetTimeWvW  = settings.DefineSetting($"{this.GetType().Name}_next_reset", DateTime.UtcNow.AddSeconds(1));
            _lastResetTimeWvW  = settings.DefineSetting($"{this.GetType().Name}_last_reset", DateTime.UtcNow);
            _killsAtResetDaily = settings.DefineSetting($"{this.GetType().Name}_kills_daily_reset", 0);
            _killsAtResetMatch = settings.DefineSetting($"{this.GetType().Name}_kills_match_reset", 0);
        }

        public override async Task Initialize()
        {
            await FileUtil.WriteAllTextAsync($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{WVW_KILLS_WEEK}", $"0{SWORDS}", false);
            await FileUtil.WriteAllTextAsync($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{WVW_KILLS_TOTAL}", $"0{SWORDS}", false);
            await FileUtil.WriteAllTextAsync($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{WVW_KILLS_DAY}", $"0{SWORDS}", false);
            await FileUtil.WriteAllTextAsync($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{WVW_RANK}", "1 : Invader", false);
        }

        private async Task UpdateRankForWvw(Account account)
        {
            if (!Gw2ApiManager.HasPermissions(new[] { TokenPermission.Account, TokenPermission.Progression })) {
                return;
            }

            var wvwRankNum = account.WvwRank;
            if (!wvwRankNum.HasValue || wvwRankNum <= 0) {
                return;
            }

            var wvwRanks    = await TaskUtil.RetryAsync(() => Gw2ApiManager.Gw2ApiClient.V2.Wvw.Ranks.AllAsync()).Unwrap();
            if (wvwRanks == null) {
                return;
            }

            var wvwRankObj = wvwRanks.MaxBy(y => wvwRankNum >= y.MinRank);
            await FileUtil.WriteAllTextAsync($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{WVW_RANK}", $"{wvwRankNum:N0} : {wvwRankObj.Title}");
        }

        private async Task<int> RequestTotalKillsForWvW()
        {
            if (!Gw2ApiManager.HasPermissions(new[] { TokenPermission.Account, TokenPermission.Progression })) {
                return -1;
            }

            var achievements = await TaskUtil.RetryAsync(() => Gw2ApiManager.Gw2ApiClient.V2.Account.Achievements.GetAsync()).Unwrap();

            if (achievements == null) {
                return -1;
            }

            var realmAvenger = achievements.FirstOrDefault(x => x.Id == 283); // Realm Avenger
            return realmAvenger?.Current ?? -1;
        }

        private async Task<bool> ResetWvWMatch(int worldId) {
            if (_lastResetTimeWvW.Value < _nextResetTimeWvW.Value && DateTime.UtcNow > _nextResetTimeWvW.Value) {
                var wvwWorldMatch = await TaskUtil.RetryAsync(() => Gw2ApiManager.Gw2ApiClient.V2.Wvw.Matches.World(worldId).GetAsync()).Unwrap();

                if (wvwWorldMatch == null) {
                    return false;
                }
                
                var totalKills = await RequestTotalKillsForWvW();

                if (totalKills < 0) {
                    return false;
                }

                _killsAtResetDaily.Value = totalKills;
                _killsAtResetMatch.Value = totalKills;
                _lastResetTimeWvW.Value  = DateTime.UtcNow;
                _nextResetTimeWvW.Value  = wvwWorldMatch.EndTime.UtcDateTime.AddMinutes(5);
            }
            return true;
        }

        protected override async Task<bool> ResetDaily()
        {
            var totalKills = await RequestTotalKillsForWvW();
            if (totalKills < 0) {
                return false;
            }
            _killsAtResetDaily.Value = totalKills;
            return true;
        }

        protected override async Task Update()
        {
            var account = StreamOutModule.Instance?.Account;
            if (account == null) {
                return;
            }

            await UpdateRankForWvw(account);

            if (!await ResetWvWMatch(account.World)) {
                return;
            }

            var prefixKills = UnicodeSigning == StreamOutModule.UnicodeSigning.Prefixed ? SWORDS : string.Empty;
            var suffixKills = UnicodeSigning == StreamOutModule.UnicodeSigning.Suffixed ? SWORDS : string.Empty;

            // WvW kills
            var totalKillsWvW = await RequestTotalKillsForWvW();

            if (totalKillsWvW < 0) {
                return;
            }

            await FileUtil.WriteAllTextAsync($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{WVW_KILLS_TOTAL}", $"{prefixKills}{totalKillsWvW}{suffixKills}");

            var killsDaily = totalKillsWvW - _killsAtResetDaily.Value;
            await FileUtil.WriteAllTextAsync($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{WVW_KILLS_DAY}",   $"{prefixKills}{killsDaily}{suffixKills}");

            var killsMatch = totalKillsWvW - _killsAtResetMatch.Value;
            await FileUtil.WriteAllTextAsync($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{WVW_KILLS_WEEK}", $"{prefixKills}{killsMatch}{suffixKills}");
        }

        public override async Task Clear()
        {
            var dir = DirectoriesManager.GetFullDirectoryPath("stream_out");
            await FileUtil.DeleteAsync(Path.Combine(dir, WVW_KILLS_WEEK));
            await FileUtil.DeleteAsync(Path.Combine(dir, WVW_KILLS_DAY));
            await FileUtil.DeleteAsync(Path.Combine(dir, WVW_KILLS_TOTAL));
            await FileUtil.DeleteAsync(Path.Combine(dir, WVW_RANK));
        }

        public override void Dispose()
        {
        }
    }
}