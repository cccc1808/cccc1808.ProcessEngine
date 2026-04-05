using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Components
{
    public class EFProcessProxyComponent<TId> : IProcessComponent<TId>
    {
        public ProcessDbEntity<TId> ProcessDbEntity { get; }

        public EFProcessProxyComponent(
            ProcessDbEntity<TId> processDbEntity)
        {
            ProcessDbEntity = processDbEntity;
        }

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
        
        public IProcessComponent<TId>.ErrorDto? Error { get; set; }

        public DateTimeOffset SelectLockTimeout 
        {
            get => ProcessDbEntity.SelectLockTimeout;
            set => ProcessDbEntity.SelectLockTimeout = value;
        }
    }
}
