namespace MyCompany.MyProduct.Apis1.Experiment1;
#pragma warning disable IDE0065 // Misplaced using directive
using MyCompany.MyProduct.Apis1.Experiment2.Sub;
#pragma warning restore IDE0065 // Misplaced using directive

public static class Class5
{
    public static void Test1() => _ = new Foo2();

    public static void Test2() => _ = new Experiment3.Sub.Foo3();

    public static void Test3() => _ = new Foo2();

    public static void Test4() => _ = new Experiment3.Sub.Foo3();
}
