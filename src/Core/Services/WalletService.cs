using Blish_HUD.Extended;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using Gw2Sharp.WebApi.V2.Models;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Nekres.Stream_Out.Core.Services {
    internal class WalletService : ExportService
    {
        private Gw2ApiManager Gw2ApiManager => StreamOutModule.Instance?.Gw2ApiManager;
        private DirectoriesManager DirectoriesManager => StreamOutModule.Instance?.DirectoriesManager;

        private const string WALLET_COINS = "wallet_coins.png";
        private const string WALLET_KARMA = "wallet_karma.png";

        public WalletService(SettingCollection settings) : base(settings) 
        {
        }

        protected override async Task Update()
        {
            await UpdateWallet();
        }

        public override async Task Initialize()
        {
            await Gw2Util.GenerateCoinsImage($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{WALLET_COINS}", 10000000, false);
            await Gw2Util.GenerateKarmaImage($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{WALLET_KARMA}", 10000000, false);
        }

        private async Task UpdateWallet()
        {
            if (!Gw2ApiManager.HasPermissions(new[] { TokenPermission.Account, TokenPermission.Wallet })) {
                return;
            }

            var wallet = await TaskUtil.RetryAsync(() => Gw2ApiManager.Gw2ApiClient.V2.Account.Wallet.GetAsync());

            if (wallet == null) {
                return;
            }

            var coins = wallet.FirstOrDefault(x => x.Id == 1)?.Value ?? 0; // Coins
            await Gw2Util.GenerateCoinsImage($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{WALLET_COINS}", coins);

            var karma = wallet.FirstOrDefault(x => x.Id == 2)?.Value ?? 0; // Karma
            await Gw2Util.GenerateKarmaImage($"{DirectoriesManager.GetFullDirectoryPath("stream_out")}/{WALLET_KARMA}", karma);
        }

        public override async Task Clear()
        {
            var dir = DirectoriesManager.GetFullDirectoryPath("stream_out");
            await FileUtil.DeleteAsync(Path.Combine(dir, WALLET_COINS));
            await FileUtil.DeleteAsync(Path.Combine(dir, WALLET_KARMA));
        }

        public override void Dispose()
        {
        }
    }
}
