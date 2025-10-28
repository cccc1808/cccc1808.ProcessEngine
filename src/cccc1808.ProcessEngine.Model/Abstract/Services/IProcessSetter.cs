using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;

namespace cccc1808.ProcessEngine.Model.Abstract.Services
{
    public interface IProcessSetter
    {
        void SetStatus<TId>(
            IProcessContainer<TId> process,
            ProcessStatusEnum status);

        void ClearError<TId>(
            IProcessContainer<TId> process);

        void SetError<TId>(
            IProcessContainer<TId> process,
            Exception ex);

        void SetTimer<TId>(
            IProcessContainer<TId> process,
            DateTimeOffset date);
    }
}
