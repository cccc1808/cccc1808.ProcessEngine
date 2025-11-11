using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Dto.Components
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
                new ProcessIdDto<TId>(
                    ProcessDbEntity.Id),
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
        public bool HaveErrorFlag 
        { 
            get => ProcessDbEntity.HaveErrorFlag; 
            set => ProcessDbEntity.HaveErrorFlag = value; 
        }
        public ProcessStatusEnum Status
        {
            get => ProcessDbEntity.Status;
            set => ProcessDbEntity.Status = value;
        }
        public short? ReTryCount
        {
            get => ProcessDbEntity.ReTryCount;
            set => ProcessDbEntity.ReTryCount = value;
        }
        public IProcessComponent<TId>.ErrorDto? Error
        {
            get => ProcessDbEntity.Error.Error.HasValue 
                ? new IProcessComponent<TId>.ErrorDto(
                    ProcessDbEntity.Error.Error.Value,
                    ProcessDbEntity.Error.ErrorSessionId.Value,
                    ProcessDbEntity.Error.ErrorDate.Value)
                : null;
            set 
            {
                if (value.HasValue)
                {
                    ProcessDbEntity.Error.Error = value.Value.ErrorJson;
                    ProcessDbEntity.Error.ErrorSessionId = value.Value.SessionId;
                    ProcessDbEntity.Error.ErrorDate = value.Value.Date;
                }
                else 
                {
                    ProcessDbEntity.Error.Error = null;
                    ProcessDbEntity.Error.ErrorSessionId = null;
                    ProcessDbEntity.Error.ErrorDate = null;
                }
            }
        }        
        public DateTimeOffset TimerDate
        {
            get => ProcessDbEntity.TimerDate;
            set => ProcessDbEntity.TimerDate = value;
        }
        public int WakeupLockCounter
        {
            get => ProcessDbEntity.WakeupLockCounter;
            set => ProcessDbEntity.WakeupLockCounter = value;
        }
    }
}
