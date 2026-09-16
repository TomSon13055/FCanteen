using System;
using System.Collections.Generic;
using System.Text;

namespace FCanteen.Lab02Runner.RaceConditions
{
    public static class IngredientRaceConditionDemo
    {
        private const int InitialStock = 100_000;

        private const int OrderCount = 50_000;

        private const int QuantityPerOrder = 1;

        public static void Run()
        {
            Console.WriteLine();
            Console.WriteLine(
                "==============================================");

            Console.WriteLine(
                "       YC5 - RACE CONDITION DEMO");

            Console.WriteLine(
                "==============================================");

            Console.WriteLine(
                $"Initial ingredient stock : {InitialStock:N0}");

            Console.WriteLine(
                $"Parallel orders          : {OrderCount:N0}");

            Console.WriteLine(
                $"Consume per order        : {QuantityPerOrder:N0}");

            int expectedStock =
                InitialStock
                - OrderCount * QuantityPerOrder;

            Console.WriteLine(
                $"Expected final stock     : {expectedStock:N0}");

            Console.WriteLine();

            // =====================================================
            // VERSION 1:
            // Không đồng bộ -> Race Condition
            // =====================================================

            int unsafeStock =
                InitialStock;

            Parallel.For(
                0,
                OrderCount,
                i =>
                {
                    int current =
                        unsafeStock;

                    Thread.SpinWait(50);

                    unsafeStock =
                        current - QuantityPerOrder;
                });

            Console.WriteLine(
                "===== WITHOUT SYNCHRONIZATION =====");

            Console.WriteLine(
                $"Expected stock : {expectedStock:N0}");

            Console.WriteLine(
                $"Actual stock   : {unsafeStock:N0}");

            Console.WriteLine(
                $"Difference     : " +
                $"{unsafeStock - expectedStock:N0}");

            Console.WriteLine(
                $"Result         : " +
                $"{(unsafeStock == expectedStock ? "CORRECT" : "WRONG")}");

            Console.WriteLine();

            // =====================================================
            // VERSION 2:
            // Đồng bộ bằng Interlocked
            // =====================================================

            int safeStock =
                InitialStock;

            Parallel.For(
                0,
                OrderCount,
                i =>
                {
                    Interlocked.Add(
                        ref safeStock,
                        -QuantityPerOrder);
                });

            Console.WriteLine(
                "===== WITH INTERLOCKED =====");

            Console.WriteLine(
                $"Expected stock : {expectedStock:N0}");

            Console.WriteLine(
                $"Actual stock   : {safeStock:N0}");

            Console.WriteLine(
                $"Difference     : " +
                $"{safeStock - expectedStock:N0}");

            Console.WriteLine(
                $"Result         : " +
                $"{(safeStock == expectedStock ? "CORRECT" : "WRONG")}");

            Console.WriteLine(
                "==============================================");
        }
    }
}
