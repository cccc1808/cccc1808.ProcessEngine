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
            = ExecuteStepByStepGroupMiddleware<TId>.OptionsDto.CreateStepSave(
                cycleLimit: 50,
                isolationMode: IIsolationService.IsolationMode.DbSavepointAndClearChangeTracker,
                useReloadAfterError: true);

        /// <summary>
        /// Preset2.
        /// Шаги не изолированы.
        /// Сохраняем в БД только в самом конце.
        /// </summary>
        public static ExecuteStepByStepGroupMiddleware<TId>.OptionsDto Preset2 { get; }
            = ExecuteStepByStepGroupMiddleware<TId>.OptionsDto.CreateEndSave(
                cycleLimit: 50,
                isolationMode: IIsolationService.IsolationMode.ClearChangeTracker,
                useReloadAfterError: true,
                // EF автоматически создаст savepoint (ему в общем то отдельный savepoint не нужен).
                endSaveOptions: new ExecuteStepByStepGroupMiddleware<TId>.EndSaveOptionsDto(
                    IsolationMode: IIsolationService.IsolationMode.DbSavepointAndClearChangeTracker
                    )
                );

        /// <summary>
        /// Preset 3.
        /// Используем изоляцию снимок ChangeTracker.
        /// Сохраняем в БД только в самом конце.
        /// </summary>
        public static ExecuteStepByStepGroupMiddleware<TId>.OptionsDto Preset3 { get; }
            = ExecuteStepByStepGroupMiddleware<TId>.OptionsDto.CreateEndSave(
                cycleLimit: 50,
                isolationMode: IIsolationService.IsolationMode.ChangeTrackerSnapshot,
                // После сброса не перезагружаем т.к. восстановлен снимок ChangeTracker.
                useReloadAfterError: false,
                // EF автоматически создаст savepoint (ему в общем то отдельный savepoint не нужен).
                endSaveOptions: new ExecuteStepByStepGroupMiddleware<TId>.EndSaveOptionsDto(
                    IsolationMode: IIsolationService.IsolationMode.DbSavepointAndClearChangeTracker
                    )
                );

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
                IIsolationService.IsolationMode.ChangeTrackerSnapshot,
                UseSave: false);
    }
}
