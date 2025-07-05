#if MY_TEST_PACKAGE_SAMPLES_DEPENDENCIES___SAMPLE___IMPORTED___A
using kevincastejon.test.package.samples.dependencies.sampleA;
#endif

namespace kevincastejon.test.package.samples.dependencies.sampleB
{
    public class B
    {
#if MY_TEST_PACKAGE_SAMPLES_DEPENDENCIES___SAMPLE___IMPORTED___A
        private A _aInstance;
#endif
    }
}