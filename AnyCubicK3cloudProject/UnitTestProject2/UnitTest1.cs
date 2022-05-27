using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AnyCubicK3cloudProject;

namespace UnitTestProject2
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            //我修改了
            ZWLF_CreateErpOrderTest zWLF_CreateErpOrderTest = new ZWLF_CreateErpOrderTest();
            zWLF_CreateErpOrderTest.CreateErpOrderTest();

        }
    }
}
