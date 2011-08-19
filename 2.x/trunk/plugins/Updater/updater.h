#pragma region libs

#pragma comment(lib, "Wininet.lib")

#pragma endregion

#pragma region enums

typedef enum _PH_UPDATER_STATE
{
	Default,
    Downloading,
    Installing
} PH_UPDATER_STATE;

#pragma endregion

#pragma region Includes

#include "phdk.h"
#include "phapppub.h"
#include "resource.h"
#include "wininet.h"
#include "mxml.h"
#include "windowsx.h"

#pragma endregion

#pragma region Defines

#define BUFFER_LEN 512
#define UPDATE_MENUITEM 1

#define Updater_SetStatusText(hwndDlg, lpString) \
	SetDlgItemText(hwndDlg, IDC_STATUSTEXT, lpString)

typedef struct _PH_UPDATER_CONTEXT
{
    HWND MainWindowHandle;
    PVOID Parameter;
} PH_UPDATER_CONTEXT, *PPH_UPDATER_CONTEXT;

typedef struct _UPDATER_XML_DATA
{
    ULONG MinorVersion;
    ULONG MajorVersion;
    PPH_STRING RelDate;
    PPH_STRING Size;
    PPH_STRING Hash;
} UPDATER_XML_DATA, *PUPDATER_XML_DATA;

#pragma endregion

#pragma region Globals

extern PPH_PLUGIN PluginInstance;

#pragma endregion

#pragma region Instances

PPH_PLUGIN PluginInstance;
PH_CALLBACK_REGISTRATION PluginMenuItemCallbackRegistration;
PH_CALLBACK_REGISTRATION MainWindowShowingCallbackRegistration;
PH_CALLBACK_REGISTRATION PluginShowOptionsCallbackRegistration;

#pragma endregion

#pragma region Functions

VOID DisposeConnection();
VOID DisposeStrings();
VOID DisposeFileHandles();

BOOL ConnectionAvailable();

BOOL ParseVersionString(
    __in PWSTR String,
    __out PULONG MajorVersion,
    __out PULONG MinorVersion
    );

LONG CompareVersions(
    __in ULONG MajorVersion1,
    __in ULONG MinorVersion1,
    __in ULONG MajorVersion2,
    __in ULONG MinorVersion2
    );

VOID StartInitialCheck();

VOID ShowUpdateDialog();

BOOL PhInstalledUsingSetup();

BOOL ReadRequestString(
    __in HINTERNET Handle,
    __out PSTR *Data,
    __out_opt PULONG DataLength
    );

BOOL QueryXmlData(
    __in PVOID Buffer,
    __out PUPDATER_XML_DATA XmlData
    );

VOID FreeXmlData(
    __in PUPDATER_XML_DATA XmlData
    );

BOOL InitializeConnection(
	__in PCWSTR host,
	__in PCWSTR path
	);

BOOL InitializeFile();

VOID LogEvent(
	__in PPH_STRING str
	);

VOID NTAPI MenuItemCallback(
    __in_opt PVOID Parameter,
    __in_opt PVOID Context
    );

VOID NTAPI MainWindowShowingCallback(
    __in_opt PVOID Parameter,
    __in_opt PVOID Context
    );

VOID NTAPI ShowOptionsCallback(
    __in_opt PVOID Parameter,
    __in_opt PVOID Context
    );

INT_PTR CALLBACK MainWndProc(      
    __in HWND hwndDlg,
    __in UINT uMsg,
    __in WPARAM wParam,
    __in LPARAM lParam
    );

INT_PTR CALLBACK OptionsDlgProc(
    __in HWND hwndDlg,
    __in UINT uMsg,
    __in WPARAM wParam,
    __in LPARAM lParam
    );

#pragma endregion