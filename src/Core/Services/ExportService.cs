using Blish_HUD.Settings;
using System;
using System.Threading.Tasks;

namespace Nekres.Stream_Out.Core.Services {
    internal abstract class ExportService : IDisposable
    {
        private DateTime _prevApiRequestTime;

        private SettingEntry<DateTime> _nextResetTimeDaily;
        private SettingEntry<DateTime> _nextResetTimeWeekly;
        private SettingEntry<DateTime> _lastResetTimeDaily;
        private SettingEntry<DateTime> _lastResetTimeWeekly;

        protected ExportService(SettingCollection settings)
        {
            _prevApiRequestTime  = DateTime.UtcNow;
            _nextResetTimeDaily  = settings.DefineSetting($"{this.GetType().Name}_nextResetDaily",  DateTime.UtcNow.AddSeconds(-1));
            _nextResetTimeWeekly = settings.DefineSetting($"{this.GetType().Name}_nextResetWeekly", DateTime.UtcNow.AddSeconds(-1));
            _lastResetTimeDaily  = settings.DefineSetting($"{this.GetType().Name}_lastResetDaily",  DateTime.UtcNow);
            _lastResetTimeWeekly = settings.DefineSetting($"{this.GetType().Name}_lastResetWeekly", DateTime.UtcNow);
        }

        public async Task DoUpdate() {
            if (DateTime.UtcNow.Subtract(_prevApiRequestTime).TotalSeconds < 300) {
                return;
            }
            _prevApiRequestTime = DateTime.UtcNow;

            if (!await DoResetDaily() || !await DoResetWeekly()) {
                return;
            }

            await this.Update();
        }

        private async Task<bool> DoResetDaily()
        {
            if (_lastResetTimeDaily.Value < _nextResetTimeDaily.Value) {
                return true;
            }


            if (!await this.ResetDaily()) {
                return false;
            }

            _lastResetTimeDaily.Value = DateTime.UtcNow;
            _nextResetTimeDaily.Value = Gw2Util.GetDailyResetTime();
            return true;
        }

        private async Task<bool> DoResetWeekly() {
            if (_lastResetTimeWeekly.Value < _nextResetTimeWeekly.Value) {
                return true;
            }

            if (!await this.ResetWeekly()) {
                return false;
            }

            _lastResetTimeWeekly.Value = DateTime.UtcNow;
            _nextResetTimeWeekly.Value = Gw2Util.GetWeeklyResetTime();
            return true;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public virtual async Task Initialize() { /* NOOP */ }

        protected virtual async Task Update() { /* NOOP */ }

        protected virtual async Task<bool> ResetDaily() { return true; }

        protected virtual async Task<bool> ResetWeekly() { return true; }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

        public abstract Task Clear();

        public abstract void Dispose();
    }
}
