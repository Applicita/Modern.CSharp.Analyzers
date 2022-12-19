// TODO: Test if we get the fully qualified namespaces and imports names
namespace Root
{
    public class RootClass { }
    
    namespace Sub1 
    {
        public class Sub1Class { }

        namespace Sub2 
        {
            public class Sub2Class 
            {
                public Sub2Class() 
                { 
                    _ = new RootClass(); 
                    _ = new Sub1Class(); 
                    _ = new Sub1Sibling.Sub1SiblingClass();
                }
            }
        }
    }

    namespace Sub1Sibling
    {
        public class Sub1SiblingClass { }
    }

    namespace Sub2Sibling
    {
        #pragma warning disable IDE0065 // Misplaced using directive
        using Sub1;
        #pragma warning restore IDE0065 // Misplaced using directive

        public class Sub2SiblingClass
        {
            public Sub2SiblingClass()
            {
                _ = new Sub1Class();
                _ = new Sub1.Sub2.Sub2Class();
            }
        }
    }
}
