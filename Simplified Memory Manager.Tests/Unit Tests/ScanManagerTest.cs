using SimplifiedMemoryManager.Tests;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplified_Memory_Manager.Tests.Unit_Tests
{
    public class ScanManagerTest
    {
        [Fact]
        public void CanInitialize()
        {
            //Arrange
            ScanManager scanManager;

            //Act
            scanManager = new ScanManager();

            //Assert
            Assert.NotNull(scanManager);
        }

        [Fact]
        public void CanInitializeWithQuantity()
        {
            //Arrange
            int quantity = 2;

            //Act
            ScanManager scanManager = new ScanManager(quantity);

            //Assert
            Assert.NotNull(scanManager);
        }

        [Fact]
        public void CanDoByteArrayScanWithBadAoB()
        {
            //Arrange
            ScanManager scanManager = new ScanManager();

            //Act
            scanManager.ByteArrayScan(RealMemory.LoadSampleMemory(), new SimplePattern(RealMemory.InvalidAoBInMemory));

            //Assert
            Assert.NotEqual(RealMemory.ValidAoBLocation, scanManager.ScanResult.FirstOrDefault());
        }

        [Fact]
        public void ByteArrayScanWithValidAoBReturnsAResult()
        {
            //Arrange
            ScanManager scanManager = new ScanManager();

            //Act
            scanManager.ByteArrayScan(RealMemory.LoadSampleMemory(), new SimplePattern(RealMemory.ValidAoBInMemory));

            //Assert
            Assert.True(scanManager.ScanResult.FirstOrDefault() > 0);
        }
    }
}
