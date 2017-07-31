using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Sandbox
{
    class Program
    {
        static void Main(string[] args)
        {

            var json = File.ReadAllText("sampleconfig.json");

            dynamic jobject = JObject.Parse(json);

            var custom = jobject.custom;

           
        }


    }
}
