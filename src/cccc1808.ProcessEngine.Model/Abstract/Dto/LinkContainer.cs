using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto
{
    public static class LinkContainer 
    { 
        public static LinkContainer<T> Create<T>(T data) 
        {
            return new LinkContainer<T>(data);
        }
    }

    public class LinkContainer<T>
    {
        public T Data { get; set; }

        public LinkContainer(T data)
        {
            Data = data;
        }
    }
}
