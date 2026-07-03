using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto
{
    public readonly record struct BitFlagDto<TEnum>
        where TEnum : struct, Enum
    {
        private static readonly ConcurrentDictionary<Type, (Func<ulong, TEnum> ToEnum, Func<TEnum, ulong> ToLong)> _castCach
            = new ConcurrentDictionary<Type, (Func<ulong, TEnum> ToEnum, Func<TEnum, ulong> ToLong)>();        

        private static (Func<ulong, TEnum> ToEnum, Func<TEnum, ulong> ToLong) BuildConverter()
        {
            return _castCach.GetOrAdd(
                typeof(TEnum),
                static (type) =>
                {
                    Func<ulong, TEnum> toEnum;
                    Func<TEnum, ulong> toLong;

                    {
                        var p = Expression.Parameter(typeof(ulong));
                        var c = Expression.ConvertChecked(p, type);
                        toEnum = Expression.Lambda<Func<ulong, TEnum>>(c, p).Compile();
                    }

                    {
                        var p = Expression.Parameter(typeof(TEnum));
                        var c = Expression.ConvertChecked(p, typeof(ulong));
                        toLong = Expression.Lambda<Func<TEnum, ulong>>(c, p).Compile();
                    }

                    return (toEnum, toLong);
                });
        }

        private readonly (Func<ulong, TEnum> ToEnum, Func<TEnum, ulong> ToLong) _converter;

        private FakeEnum InnerBitsEnum { get; }

        public ulong Bits { get; }

        public TEnum BitsEnum { get; }

        private BitFlagDto(FakeEnum enumBits)
        {
            _converter = BuildConverter();

            InnerBitsEnum = enumBits;
            Bits = (ulong)enumBits;
            BitsEnum = _converter.ToEnum(Bits);
        }

        public BitFlagDto(ulong bits)
        {
            _converter = BuildConverter();

            InnerBitsEnum = (FakeEnum)bits;
            Bits = bits;
            BitsEnum = _converter.ToEnum(Bits);
        }

        public BitFlagDto(TEnum enumValue)
        {
            _converter = BuildConverter();

            Bits = _converter.ToLong(enumValue);
            InnerBitsEnum = (FakeEnum)Bits;            
            BitsEnum = enumValue;
        }

        public bool ContainsFlag(ulong value)
        {
            var enumValue = (FakeEnum)value;
            return (InnerBitsEnum & enumValue) == enumValue;
        }

        public bool ContainsFlag(TEnum value)
        {
            return ContainsFlag(
                new BitFlagDto<TEnum>(value));
        }

        public bool ContainsFlag(in BitFlagDto<TEnum> value)
        {
            return (InnerBitsEnum & value.InnerBitsEnum) == value.InnerBitsEnum;
        }

        public BitFlagDto<TEnum> AddFlag(ulong value)
        {
            var enumValue = (FakeEnum)value;
            return new BitFlagDto<TEnum>(InnerBitsEnum | enumValue);
        }

        public BitFlagDto<TEnum> AddFlag(TEnum value)
        {
            return AddFlag(new BitFlagDto<TEnum>(value));
        }

        public BitFlagDto<TEnum> AddFlag(in BitFlagDto<TEnum> value)
        {
            return new BitFlagDto<TEnum>(InnerBitsEnum | value.InnerBitsEnum);
        }

        public BitFlagDto<TEnum> RemoveFlag(ulong value)
        {
            var enumValue = (FakeEnum)value;
            return new BitFlagDto<TEnum>(InnerBitsEnum ^ enumValue);
        }

        public BitFlagDto<TEnum> RemoveFlag(TEnum value)
        {
            return RemoveFlag(new BitFlagDto<TEnum>(value));
        }

        public BitFlagDto<TEnum> RemoveFlag(in BitFlagDto<TEnum> value)
        {
            return new BitFlagDto<TEnum>(InnerBitsEnum ^ value.InnerBitsEnum);
        }

        public override int GetHashCode()
        {
            return Bits.GetHashCode();
        }

        public override string ToString()
        {
            return Bits.ToString();
        }

        [Flags]
        private enum FakeEnum { }
    }

    public readonly record struct BitFlagDto
    {
        private FakeEnum InnerBitsEnum { get; }

        public ulong Bits { get; }

        public bool IsEmpty { get; }

        private BitFlagDto(FakeEnum enumBits)
        {
            InnerBitsEnum = enumBits;
            Bits = (ulong)enumBits;
            IsEmpty = Bits == 0;
        }

        public BitFlagDto(ulong bits)
        {
            InnerBitsEnum = (FakeEnum)bits;
            Bits = bits;
        }

        public bool ContainsFlag(ulong value)
        {
            var enumValue = (FakeEnum)value;
            return (InnerBitsEnum & enumValue) == enumValue;
        }

        public bool ContainsFlag(in BitFlagDto value)
        {
            return (InnerBitsEnum & value.InnerBitsEnum) == value.InnerBitsEnum;
        }

        public BitFlagDto AddFlag(ulong value) 
        {
            var enumValue = (FakeEnum)value;
            return new BitFlagDto(InnerBitsEnum | enumValue);
        }

        public BitFlagDto AddFlag(in BitFlagDto value)
        {
            return new BitFlagDto(InnerBitsEnum | value.InnerBitsEnum);
        }

        public BitFlagDto RemoveFlag(ulong value) 
        {
            var enumValue = (FakeEnum)value;
            return new BitFlagDto(InnerBitsEnum ^ enumValue);
        }

        public BitFlagDto RemoveFlag(in BitFlagDto value)
        {
            return new BitFlagDto(InnerBitsEnum ^ value.InnerBitsEnum);
        }

        public override int GetHashCode()
        {
            return Bits.GetHashCode();
        }

        public override string ToString()
        {
            return Bits.ToString();
        }

        [Flags]
        private enum FakeEnum { }

        public static BitFlagDto Empty 
            => new BitFlagDto(bits: 0);
    }
}
