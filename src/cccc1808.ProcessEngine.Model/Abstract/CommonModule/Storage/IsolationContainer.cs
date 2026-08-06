using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage
{
    public static class IsolationContainer
    {
        public static int TransactionIsolationIndex { get; }
            = 0;
    }

    /// <summary>
    /// Контейнер для накапливания элементов, 
    /// который должны быть либо вручную выполнены после коммита либо сброшены при компенсации.
    /// IIsolationService или ITransactionManager.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class IsolationContainer<T>
    {
        /// <summary>
        /// Буфер элементов.
        /// Индекс - идентефикатор Scope (для простой и быстрой упорядоченности, плюс генерация позволяет).
        /// </summary>
        private List<List<T>> _buffer;

        /// <summary>
        /// Получить накомпленные элементы (которые не были скомпенсированы).
        /// </summary>
        public IEnumerable<T> All 
            => _buffer.SelectMany(e => e);

        public IsolationContainer(int scopeCapacity)
        {
            _buffer = new List<List<T>>(scopeCapacity);
        }

        public void IncreseCapacity(int scopeIndex, int capacity)
        {
            var scopeBuffer = GetOrInitScopeBuffer(scopeIndex);
            scopeBuffer.EnsureCapacity(scopeBuffer.Count + capacity);
        }

        public void Add(int scopeIndex, T elem)
        {
            // TODO: Доработк порядка в scope транзакции (0) (фазы: начало трназакции -> isolation scopes -> конец трназакции).

            var scopeBuffer = GetOrInitScopeBuffer(scopeIndex);
            scopeBuffer.Add(elem);
        }

        public void AddRange(int scopeIndex, ICollection<T> data)
        {
            // Если scope не инициализирован, то создаем буфер под него.
            //if (_buffer.Count == scopeIndex)
            //{
            //    _buffer.Add(new List<T>(0));
            //}
            var scopeBuffer = GetOrInitScopeBuffer(scopeIndex);
            scopeBuffer.AddRange(data);
        }

        /// <summary>
        /// Scope был скомпенсирован (его элементы удаляются - не будут выполнятся).
        /// </summary>
        public void ScopeCompensated(int scopeIndex)
        {
            if (_buffer.Count > scopeIndex)
            {
                _buffer[scopeIndex].Clear();
            }
        }

        public void Clear()
        {
            foreach (var elem in _buffer) 
            {
                elem.Clear();
            }
            _buffer.Clear();
        }

        private List<T> GetOrInitScopeBuffer(int scopeIndex)
        {
            // Если scope не инициализирован, то создаем буфер под него.
            if (_buffer.Count <= scopeIndex)
            {
                _buffer.AddRange(
                    Enumerable.Repeat(false, (scopeIndex - _buffer.Count) + 1)
                        .Select(_ => new List<T>(0))
                        );
            }

            return _buffer[scopeIndex];
        }
    }
}
