using MyCompany.MyProduct.Apis1.Experiment2.Sub;
namespace MyCompany.MyProduct.Apis1;

public static class Class1
{
    public static void Test1() => _ = new Foo2();

    public static void Test2() => _ = new Experiment3.Sub.Foo3();

    public static void Test3() => _ = new Foo2();

    public static void Test4() => _ = new Experiment3.Sub.Foo3();
}
