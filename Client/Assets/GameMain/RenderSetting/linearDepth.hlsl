//UNITY_SHADER_NO_UPGRADE
#ifndef MYHLSLINCLUDE_INCLUDED
#define MYHLSLINCLUDE_INCLUDED
// Values used to linearize the Z buffer (http://www.humus.name/temp/Linearize%20depth.txt)
    // x = 1-far/near
    // y = far/near
    // z = x/far
    // w = y/far
    // or in case of a reversed depth buffer (UNITY_REVERSED_Z is 1)
    // x = -1+far/near
    // y = 1
    // z = x/far
    // w = 1/far
void LocalLinear01Depth_float(float A, float far,float near, out float linearDepth)
{
    A = 1.0 / ((1-far/near) * A + far/near);
    linearDepth = A;
}
void LocalLinear01Depth_half(float A, float far,float near, out float linearDepth)
{
    A = 1.0 / ((1-far/near) * A + far/near);
    linearDepth = A;
}
#endif //MYHLSLINCLUDE_INCLUDED