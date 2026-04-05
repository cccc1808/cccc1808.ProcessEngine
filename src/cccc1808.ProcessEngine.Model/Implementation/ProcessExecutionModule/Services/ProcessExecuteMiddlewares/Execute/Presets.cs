using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute
{
    /// <summary>
    /// Замечание: относитесь аккуратно к выбору конфигурации.
    /// Это влияет на производительность, целостность, обработку ошибок.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public static class Presets<TId>
    {
        /// <summary>
        /// Preset1.
        /// Используем Db Savepoint.
        /// Сохраняем в БД после шага.
        /// </summary>
        public static ExecuteStepByStepGroupMiddleware<TId>.OptionsDto Preset1 { get; }
            = new ExecuteStepByStepGroupMiddleware<TId>.OptionsDto(
                CycleLimit: 50,
                IsolationMode: IIsolationService.IsolationMode.DbSavepointAndClearChangeTracker,
                UseAfterStepSave: true,
                UseEndSave: false,
                UseReloadAfterError: true);

        /// <summary>
        /// Preset2.
        /// Используем ChangeTracker.
        /// Сохраняем в БД только в самом конце.
        /// </summary>
        public static ExecuteStepByStepGroupMiddleware<TId>.OptionsDto Preset2 { get; }
            = new ExecuteStepByStepGroupMiddleware<TId>.OptionsDto(
                CycleLimit: 50,
                IsolationMode: IIsolationService.IsolationMode.ClearChangeTracker,
                UseAfterStepSave: false,
                UseEndSave: true,
                UseReloadAfterError: true);

        public static BaseSingleProcessHandler<TId>.OptionsDto Preset1_Single { get; }
            = new BaseSingleProcessHandler<TId>.OptionsDto(
                Preset1,
                IIsolationService.IsolationMode.DbSavepointAndClearChangeTracker,
                UseSave: true);

        /// <summary>
        /// При разбивке на батчи по-одному изоляция внутри не нужна.
        /// </summary>
        public static BaseSingleProcessHandler<TId>.OptionsDto Preset1_Single_M { get; }
            = new BaseSingleProcessHandler<TId>.OptionsDto(
                Preset1,
                IIsolationService.IsolationMode.No,
                UseSave: false);

        public static BaseSingleProcessHandler<TId>.OptionsDto Preset2_Single { get; }
            = new BaseSingleProcessHandler<TId>.OptionsDto(
                Preset2,
                IIsolationService.IsolationMode.ChangeTrackerSnapshotAndManual,
                UseSave: false);
    }
}
