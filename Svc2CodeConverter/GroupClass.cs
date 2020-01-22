using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Svc2CodeConverter
{
    public class GroupClass
    {
        public string Name { get; set; }

        public bool Map { get; set; }

        public GroupClass(string name, bool map = true)
        {
            Name = name; Map = map;
            Members = new List<GroupClass>();
        }

        public List<GroupClass> Members { get; set; } 

        /*T Childrens { get; set; }

        public GroupClass(string name, T obj)
        {
            Childrens = obj;
            Name = name;
        }

        public T GetChilds()
        {
            return Childrens;
        }*/
    }
}
