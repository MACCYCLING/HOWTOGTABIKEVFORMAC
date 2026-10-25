/* Companion to Wattline's virtual USBXpress ANT stick, not a hardware driver.
 * ANT_Receiver constructs this manufacturing interface before SI_Open.
 * ABI verified against Dynastream DSI_CP210xManufacturingDLL_3_1.h.
 * Only identification and virtual-reset interfaces are exported; no firmware
 * programming interfaces or physical USB access are present.
 */
#include <windows.h>
#include <string.h>
#include <stdio.h>
#define EXPORT __declspec(dllexport) int WINAPI
#define SUCCESS 0
#define INVALID_HANDLE_STATUS 1
#define INVALID_PARAMETER_STATUS 2
static int identify(HANDLE h,void *out,BYTE *length,BOOL ascii,const char *value) {
    if(!h)return INVALID_HANDLE_STATUS;
    if(!out||!length)return INVALID_PARAMETER_STATUS;
    if(ascii) {
        strcpy((char *)out,value);
        *length=(BYTE)strlen(value);
    } else {
        int n=MultiByteToWideChar(CP_ACP,0,value,-1,(WCHAR *)out,63);
        if(!n)return INVALID_PARAMETER_STATUS;
        *length=(BYTE)(n-1);
    }
    return SUCCESS;
}
EXPORT CP210x_GetDeviceProductString(HANDLE h,LPVOID out,LPBYTE length,BOOL ascii) {
    return identify(h,out,length,ascii,"ANT USBStick2");
}
EXPORT CP210x_GetDeviceSerialNumber(HANDLE h,LPVOID out,LPBYTE length,BOOL ascii) {
    return identify(h,out,length,ascii,"11170001");
}
EXPORT CP210x_GetDeviceVid(HANDLE h,LPWORD out) {
    if(!h)return INVALID_HANDLE_STATUS;
    if(!out)return INVALID_PARAMETER_STATUS;
    *out=0x0fcf;return SUCCESS;
}
EXPORT CP210x_GetDevicePid(HANDLE h,LPWORD out) {
    if(!h)return INVALID_HANDLE_STATUS;
    if(!out)return INVALID_PARAMETER_STATUS;
    *out=0x1008;return SUCCESS;
}
EXPORT CP210x_Reset(HANDLE h) {
    if(!h)return INVALID_HANDLE_STATUS;
    /* Reset the virtual ANT protocol state over the existing socket, rather
     * than touching hardware. The packet is ANT SYSTEM_RESET, checksum EF. */
    HMODULE si=GetModuleHandleA("DSI_SiUSBXp_3_1.DLL");
    typedef DWORD (WINAPI *write_fn)(HANDLE,LPVOID,DWORD,LPDWORD,LPVOID);
    write_fn write=si?(write_fn)GetProcAddress(si,"SI_Write"):NULL;
    if(!write)return INVALID_HANDLE_STATUS;
    unsigned char reset[]={0xa4,0x01,0x4a,0x00,0xef};
    DWORD written=0;
    return write(h,reset,sizeof(reset),&written,NULL)==0 && written==sizeof(reset)?SUCCESS:3;
}
