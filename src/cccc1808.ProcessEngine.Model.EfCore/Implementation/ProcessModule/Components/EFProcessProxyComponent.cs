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
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Components
{
    public class EFProcessProxyComponent<TId> : IProcessComponent<TId>
    {
        public ProcessDbEntity<TId> ProcessDbEntity { get; }        

        public ProcessInstanceInfoDto<TId> Info 
        { 
            get => new ProcessInstanceInfoDto<TId>(
                ProcessDbEntity.Id,
                new ProcessTypeDto(
                    ProcessDbEntity.ProcessTypeId, 
                    ProcessDbEntity.ProcessVersion),
                ProcessDbEntity.Priority
                );
            set 
            {
                ProcessDbEntity.ProcessTypeId = value.ProcessType.ProcessType;
                ProcessDbEntity.ProcessVersion = value.ProcessType.ProcessVersion;
                ProcessDbEntity.Priority = value.Priority;
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

        public DateTimeOffset ReservationTimeout 
        {
            get => ProcessDbEntity.ReservationTimeout;
            set => ProcessDbEntity.ReservationTimeout = value;
        }

        /// <summary>
        /// TODO: такая реализация при использовании изоляции <see cref="IChangeTrackerCompensateService"/> не откатывается.
        /// Критично ли это?.
        /// </summary>
        public IProcessComponent<TId>.ErrorDto? Error { get; set; }

        public BitFlagDto SignalCode { get => new BitFlagDto(ProcessDbEntity.SignalCode); set => ProcessDbEntity.SignalCode = value.Bits; }

        public EFProcessProxyComponent(
            ProcessDbEntity<TId> processDbEntity)
        {
            ProcessDbEntity = processDbEntity;
        }
    }
}
