#if MY_TEST_PACKAGE_SAMPLES_DEPENDENCIES___SAMPLE___IMPORTED___B
using kevincastejon.test.package.samples.dependencies.sampleB;
#endif

namespace kevincastejon.test.package.samples.dependencies.sampleC
{
    public class C
    {
#if MY_TEST_PACKAGE_SAMPLES_DEPENDENCIES___SAMPLE___IMPORTED___B
        private B _bInstance;
#endif
    }
}