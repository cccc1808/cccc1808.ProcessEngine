using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Components
{
    public class EFProcessProxyComponent<TId> 
        : IProcessComponent<TId>
    {
        public ProcessDbEntity<TId> ProcessDbEntity { get; }        

        private ProcessInstanceInfoDto<TId> InnerInfo { get; set; }

        public ProcessInstanceInfoDto<TId> Info 
        {
            get => InnerInfo; 
            set 
            {
                if (ProcessDbEntity.ProcessTypeId != value.Registry.Unique.ProcessType.ProcessType)
                {
                    throw new ArgumentException("Изменение типа процесса не допустимо.");
                }
                if (ProcessDbEntity.ProcessVersion != value.Registry.Unique.ProcessType.ProcessVersion)
                {
                    throw new ArgumentException("Изменение версии типа процесса не допустимо.");
                }

                ProcessDbEntity.Priority = value.Registry.Unique.Priority;
                InnerInfo = value;
            }
        }

        public bool StoppedByError 
        { 
            get => ProcessDbEntity.StoppedByError; 
            set => ProcessDbEntity.StoppedByError = value; 
        }

        public ProcessStatusEnum Status
        {
            get => ProcessDbEntity.Status;
            set => ProcessDbEntity.Status = value;
        }

        public short? RetryCount
        {
            get => ProcessDbEntity.RetryCount;
            set => ProcessDbEntity.RetryCount = value;
        }

        /// <summary>
        /// TODO: такая реализация при использовании изоляции <see cref="IChangeTrackerCompensateService"/> не откатывается.
        /// Критично ли это?.
        /// </summary>
        public IProcessComponent<TId>.ErrorDto? Error { get; set; }

        public EFProcessProxyComponent(
            ProcessDbEntity<TId> processDbEntity,
            ProcessRegistryDto processRegistryDto)
        {
            ProcessDbEntity = processDbEntity;
            Info = new ProcessInstanceInfoDto<TId>(processDbEntity.Id, processRegistryDto);
        }
    }
}
