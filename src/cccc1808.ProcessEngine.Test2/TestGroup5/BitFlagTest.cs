using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;

using Shouldly;

namespace cccc1808.ProcessEngine.Test2.TestGroup5
{
    public class BitFlagTest
    {
        [Fact]
        public void Test1()
        {
            var flag = new BitFlagDto<TestEnum>();

            flag.ContainsFlag(TestEnum.Flag1).ShouldBeFalse();

            flag = flag.AddFlag(TestEnum.Flag1);
            flag.ContainsFlag(TestEnum.Flag1).ShouldBeTrue();

            flag = flag.AddFlag(TestEnum.Flag2);
            flag.ContainsFlag(TestEnum.Flag1).ShouldBeTrue();
            flag.ContainsFlag(TestEnum.Flag2).ShouldBeTrue();

            var flag2 = new BitFlagDto<TestEnum>();
            flag2 = flag2.AddFlag(flag);
            flag2.ContainsFlag(TestEnum.Flag1).ShouldBeTrue();
            flag2.ContainsFlag(TestEnum.Flag2).ShouldBeTrue();

            flag = flag.RemoveFlag(TestEnum.Flag1);
            flag.ContainsFlag(TestEnum.Flag1).ShouldBeFalse();
            flag.ContainsFlag(TestEnum.Flag2).ShouldBeTrue();            
        }

        [Fact]
        public void Test2()
        {
            var flag1 = new BitFlagDto<TestEnum>()
                .AddFlag(TestEnum.Flag1)
                .AddFlag(TestEnum.Flag2)
                .AddFlag(TestEnum.Flag3);

            flag1.RemoveFlag(flag1).Bits.ShouldBe(0u);
        }

        [Flags]
        private enum TestEnum
        {
            Flag1 = 2,
            Flag2 = 4,
            Flag3 = 8,
            Flag4 = 16,
            Flag5 = 32,
            Flag6 = 64,
            Flag7 = 128,
        }
    }
}
