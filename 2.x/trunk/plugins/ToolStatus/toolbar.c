/*
 * Process Hacker ToolStatus -
 *   main toolbar
 *
 * Copyright (C) 2011-2015 dmex
 *
 * This file is part of Process Hacker.
 *
 * Process Hacker is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Process Hacker is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with Process Hacker.  If not, see <http://www.gnu.org/licenses/>.
 */

#include "toolstatus.h"

TBBUTTON ToolbarButtons[] =
{
    // Default toolbar buttons (displayed)
    { 0, PHAPP_ID_VIEW_REFRESH, TBSTATE_ENABLED, BTNS_BUTTON | BTNS_AUTOSIZE | BTNS_SHOWTEXT, { 0 }, 0, 0 },
    { 1, PHAPP_ID_HACKER_OPTIONS, TBSTATE_ENABLED, BTNS_BUTTON | BTNS_AUTOSIZE | BTNS_SHOWTEXT, { 0 }, 0, 0 },
    { 0, 0, 0, BTNS_SEP, { 0 }, 0, 0 },
    { 2, PHAPP_ID_HACKER_FINDHANDLESORDLLS, TBSTATE_ENABLED, BTNS_BUTTON | BTNS_AUTOSIZE | BTNS_SHOWTEXT, { 0 }, 0, 0 },
    { 3, PHAPP_ID_VIEW_SYSTEMINFORMATION, TBSTATE_ENABLED, BTNS_BUTTON | BTNS_AUTOSIZE | BTNS_SHOWTEXT, { 0 }, 0, 0 },
    { 0, 0, 0, BTNS_SEP, { 0 }, 0, 0 },
    { 4, TIDC_FINDWINDOW, TBSTATE_ENABLED, BTNS_BUTTON | BTNS_AUTOSIZE | BTNS_SHOWTEXT, { 0 }, 0, 0 },
    { 5, TIDC_FINDWINDOWTHREAD, TBSTATE_ENABLED, BTNS_BUTTON | BTNS_AUTOSIZE | BTNS_SHOWTEXT, { 0 }, 0, 0 },
    { 6, TIDC_FINDWINDOWKILL, TBSTATE_ENABLED, BTNS_BUTTON | BTNS_AUTOSIZE | BTNS_SHOWTEXT, { 0 }, 0, 0 },
    // Available toolbar buttons (hidden)
    { 7, PHAPP_ID_VIEW_ALWAYSONTOP, TBSTATE_ENABLED, BTNS_BUTTON | BTNS_AUTOSIZE | BTNS_SHOWTEXT, { 0 }, 0, 0 }
};

// NOTE: This Registry key is never created or used unless the Toolbar is customized.
TBSAVEPARAMSW ToolbarSaveParams =
{
    HKEY_CURRENT_USER,
    L"Software\\ProcessHacker",
    L"ToolbarSettings"
};

static VOID RebarAddMenuItem(
    _In_ HWND WindowHandle,
    _In_ HWND HwndHandle,
    _In_ UINT cyMinChild,
    _In_ UINT cxMinChild
    )
{
    static UINT bandID = 0;

    REBARBANDINFO rebarBandInfo = { REBARBANDINFO_V6_SIZE };
    rebarBandInfo.fMask = RBBIM_STYLE | RBBIM_ID | RBBIM_CHILD | RBBIM_CHILDSIZE;
    rebarBandInfo.fStyle = RBBS_NOGRIPPER | RBBS_FIXEDSIZE;

    rebarBandInfo.wID = bandID++;
    rebarBandInfo.hwndChild = HwndHandle;
    rebarBandInfo.cyMinChild = cyMinChild;
    rebarBandInfo.cxMinChild = cxMinChild;

    SendMessage(WindowHandle, RB_INSERTBAND, (WPARAM)-1, (LPARAM)&rebarBandInfo);
}

static VOID RebarLoadSettings(
    VOID
    )
{
    static HIMAGELIST toolBarImageList = NULL;

    // Initialize the Toolbar Imagelist.
    if (EnableToolBar && !toolBarImageList)
    {
        HBITMAP arrowIconBitmap = NULL;
        HBITMAP cogIconBitmap = NULL;
        HBITMAP findIconBitmap = NULL;
        HBITMAP chartIconBitmap = NULL;
        HBITMAP appIconBitmap = NULL;
        HBITMAP goIconBitmap = NULL;
        HBITMAP crossIconBitmap = NULL;

        // Create the toolbar imagelist
        toolBarImageList = ImageList_Create(16, 16, ILC_COLOR32 | ILC_MASK, 0, 0);
        // Set the number of images
        ImageList_SetImageCount(toolBarImageList, 8);

        // Add the images to the imagelist
        if (arrowIconBitmap = LoadImageFromResources(16, 16, MAKEINTRESOURCE(IDB_ARROW_REFRESH)))
        {
            ImageList_Replace(toolBarImageList, 0, arrowIconBitmap, NULL);
            DeleteObject(arrowIconBitmap);
        }
        else
        {
            PhSetImageListBitmap(toolBarImageList, 0, (HINSTANCE)PluginInstance->DllBase, MAKEINTRESOURCE(IDB_ARROW_REFRESH_BMP));
        }

        if (cogIconBitmap = LoadImageFromResources(16, 16, MAKEINTRESOURCE(IDB_COG_EDIT)))  
        {
            ImageList_Replace(toolBarImageList, 1, cogIconBitmap, NULL);
            DeleteObject(cogIconBitmap);
        }
        else
        {
            PhSetImageListBitmap(toolBarImageList, 1, (HINSTANCE)PluginInstance->DllBase, MAKEINTRESOURCE(IDB_COG_EDIT_BMP));  
        }

        if (findIconBitmap = LoadImageFromResources(16, 16, MAKEINTRESOURCE(IDB_FIND)))
        {
            ImageList_Replace(toolBarImageList, 2, findIconBitmap, NULL);
            DeleteObject(findIconBitmap);
        }
        else
        {
            PhSetImageListBitmap(toolBarImageList, 2, (HINSTANCE)PluginInstance->DllBase, MAKEINTRESOURCE(IDB_FIND_BMP));
        }

        if (chartIconBitmap = LoadImageFromResources(16, 16, MAKEINTRESOURCE(IDB_CHART_LINE)))
        {
            ImageList_Replace(toolBarImageList, 3, chartIconBitmap, NULL);
            DeleteObject(chartIconBitmap);
        }
        else
        {
            PhSetImageListBitmap(toolBarImageList, 3, (HINSTANCE)PluginInstance->DllBase, MAKEINTRESOURCE(IDB_CHART_LINE_BMP));
        }

        if (appIconBitmap = LoadImageFromResources(16, 16, MAKEINTRESOURCE(IDB_APPLICATION)))
        {
            ImageList_Replace(toolBarImageList, 4, appIconBitmap, NULL);
            DeleteObject(appIconBitmap);
        }
        else
        {
            PhSetImageListBitmap(toolBarImageList, 4, (HINSTANCE)PluginInstance->DllBase, MAKEINTRESOURCE(IDB_APPLICATION_BMP));
        }

        if (goIconBitmap = LoadImageFromResources(16, 16, MAKEINTRESOURCE(IDB_APPLICATION_GO)))
        {
            ImageList_Replace(toolBarImageList, 5, goIconBitmap, NULL);
            DeleteObject(goIconBitmap);
        }
        else
        {
            PhSetImageListBitmap(toolBarImageList, 5, (HINSTANCE)PluginInstance->DllBase, MAKEINTRESOURCE(IDB_APPLICATION_GO_BMP));
        }

        if (crossIconBitmap = LoadImageFromResources(16, 16, MAKEINTRESOURCE(IDB_CROSS)))
        {
            ImageList_Replace(toolBarImageList, 6, crossIconBitmap, NULL);
            DeleteObject(crossIconBitmap);
        }
        else
        {
            PhSetImageListBitmap(toolBarImageList, 6, (HINSTANCE)PluginInstance->DllBase, MAKEINTRESOURCE(IDB_CROSS_BMP));
        }       

        if (crossIconBitmap = LoadImageFromResources(16, 16, MAKEINTRESOURCE(IDB_APPLICATION_GET)))
        {
            ImageList_Replace(toolBarImageList, 7, crossIconBitmap, NULL);
            DeleteObject(crossIconBitmap);
        }
        else
        {
            PhSetImageListBitmap(toolBarImageList, 7, (HINSTANCE)PluginInstance->DllBase, MAKEINTRESOURCE(IDB_APPLICATION_GET_BMP));
        }
    }

    // Load the Rebar, Toolbar and Searchbox controls.
    if (EnableToolBar && !RebarHandle)
    {
        REBARINFO rebarInfo = { sizeof(REBARINFO) };

        // Create the ReBar window.
        RebarHandle = CreateWindowEx(
            0,
            REBARCLASSNAME,
            NULL,
            WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS | WS_CLIPCHILDREN | CCS_NODIVIDER | CCS_TOP | RBS_VARHEIGHT, //RBS_FIXEDORDER | RBS_DBLCLKTOGGLE 
            CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT,
            PhMainWndHandle,
            NULL,
            (HINSTANCE)PluginInstance->DllBase,
            NULL
            );

        // Set the toolbar info with no imagelist.
        SendMessage(RebarHandle, RB_SETBARINFO, 0, (LPARAM)&rebarInfo);

        // Create the ToolBar window.
        ToolBarHandle = CreateWindowEx(
            0,
            TOOLBARCLASSNAME,
            NULL,
            WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS | WS_CLIPCHILDREN | CCS_NORESIZE | CCS_NODIVIDER | CCS_ADJUSTABLE | TBSTYLE_FLAT | TBSTYLE_LIST | TBSTYLE_TRANSPARENT | TBSTYLE_TOOLTIPS | TBSTYLE_AUTOSIZE, // TBSTYLE_ALTDRAG
            CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT,
            RebarHandle,
            NULL,
            (HINSTANCE)PluginInstance->DllBase,
            NULL
            );

        // Manually add button strings via TB_ADDSTRING.
        // NOTE: The Toolbar will sometimes decide to free strings hard-coded via (INT_PTR)L"String" 
        //       in the ToolbarButtons array causing random crashes unless we manually add the strings 
        //       into the Toolbar string pool (this bug only affects 64bit Windows)... WTF???
        ToolbarButtons[0].iString = SendMessage(ToolBarHandle, TB_ADDSTRING, 0, (LPARAM)L"Refresh");
        ToolbarButtons[1].iString = SendMessage(ToolBarHandle, TB_ADDSTRING, 0, (LPARAM)L"Options");
        ToolbarButtons[3].iString = SendMessage(ToolBarHandle, TB_ADDSTRING, 0, (LPARAM)L"Find Handles or DLLs");
        ToolbarButtons[4].iString = SendMessage(ToolBarHandle, TB_ADDSTRING, 0, (LPARAM)L"System Information");
        ToolbarButtons[6].iString = SendMessage(ToolBarHandle, TB_ADDSTRING, 0, (LPARAM)L"Find Window");
        ToolbarButtons[7].iString = SendMessage(ToolBarHandle, TB_ADDSTRING, 0, (LPARAM)L"Find Window and Thread");
        ToolbarButtons[8].iString = SendMessage(ToolBarHandle, TB_ADDSTRING, 0, (LPARAM)L"Find Window and Kill");
        ToolbarButtons[9].iString = SendMessage(ToolBarHandle, TB_ADDSTRING, 0, (LPARAM)L"Always on Top");

        // Set the toolbar struct size.
        SendMessage(ToolBarHandle, TB_BUTTONSTRUCTSIZE, sizeof(TBBUTTON), 0);
        // Set the toolbar extended toolbar styles.
        SendMessage(ToolBarHandle, TB_SETEXTENDEDSTYLE, 0, TBSTYLE_EX_DOUBLEBUFFER | TBSTYLE_EX_MIXEDBUTTONS | TBSTYLE_EX_HIDECLIPPEDBUTTONS);
        // Configure the toolbar imagelist.
        SendMessage(ToolBarHandle, TB_SETIMAGELIST, 0, (LPARAM)toolBarImageList);
        // Add the buttons to the toolbar (also specifying the default number of items to display).
        SendMessage(ToolBarHandle, TB_ADDBUTTONS, MAX_DEFAULT_TOOLBAR_ITEMS, (LPARAM)ToolbarButtons);
        // Restore the toolbar settings.
        SendMessage(ToolBarHandle, TB_SAVERESTORE, FALSE, (LPARAM)&ToolbarSaveParams);

        // Enable theming:
        //SendMessage(ReBarHandle, RB_SETWINDOWTHEME, 0, (LPARAM)L"Communications"); //Media/Communications/BrowserTabBar/Help   
        //SendMessage(ToolBarHandle, TB_SETWINDOWTHEME, 0, (LPARAM)L"Communications"); //Media/Communications/BrowserTabBar/Help

        // HACK: Query the toolbar width/height.
        ULONG_PTR toolbarButtonSize = (ULONG_PTR)SendMessage(ToolBarHandle, TB_GETBUTTONSIZE, 0, 0);

        // Inset the toolbar into the rebar control.
        RebarAddMenuItem(RebarHandle, ToolBarHandle, HIWORD(toolbarButtonSize), LOWORD(toolbarButtonSize));

        if (EnableSearchBox && !SearchboxHandle)
        {  
            SearchboxText = PhReferenceEmptyString();

            ProcessTreeFilterEntry = PhAddTreeNewFilter(PhGetFilterSupportProcessTreeList(), (PPH_TN_FILTER_FUNCTION)ProcessTreeFilterCallback, NULL);
            ServiceTreeFilterEntry = PhAddTreeNewFilter(PhGetFilterSupportServiceTreeList(), (PPH_TN_FILTER_FUNCTION)ServiceTreeFilterCallback, NULL);
            NetworkTreeFilterEntry = PhAddTreeNewFilter(PhGetFilterSupportNetworkTreeList(), (PPH_TN_FILTER_FUNCTION)NetworkTreeFilterCallback, NULL);

            // Insert a paint region into the edit control NC window area
            SearchboxHandle = CreateSearchControl(ID_SEARCH_CLEAR);

            // Insert the edit control into the rebar control
            RebarAddMenuItem(RebarHandle, SearchboxHandle, 20, 180);
        }
    }

    // Load the Statusbar control.
    if (EnableStatusBar && !StatusBarHandle)
    {
        // Create the StatusBar window.
        StatusBarHandle = CreateWindowEx(
            0,
            STATUSCLASSNAME,
            NULL,
            WS_CHILD | WS_VISIBLE | CCS_BOTTOM | SBARS_SIZEGRIP | SBARS_TOOLTIPS,
            CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT,
            PhMainWndHandle,
            NULL,
            (HINSTANCE)PluginInstance->DllBase,
            NULL
            );
    }
  
    // Hide or show controls (Note: don't unload or remove at runtime).
    if (EnableToolBar)
    {
        if (RebarHandle && !IsWindowVisible(RebarHandle))
            ShowWindow(RebarHandle, SW_SHOW);
    }
    else
    {
        if (RebarHandle && IsWindowVisible(RebarHandle))
            ShowWindow(RebarHandle, SW_HIDE);
    }

    if (EnableSearchBox)
    {
        if (SearchboxHandle && !IsWindowVisible(SearchboxHandle))
            ShowWindow(SearchboxHandle, SW_SHOW);
    }
    else
    {
        if (SearchboxHandle)
        {
            // Clear search text and reset search filters.
            SetFocus(SearchboxHandle);
            Static_SetText(SearchboxHandle, L"");

            if (IsWindowVisible(SearchboxHandle))
                ShowWindow(SearchboxHandle, SW_HIDE);
        }
    }

    if (EnableStatusBar)
    {  
        if (StatusBarHandle && !IsWindowVisible(StatusBarHandle))
            ShowWindow(StatusBarHandle, SW_SHOW);
    }
    else
    {        
        if (StatusBarHandle && IsWindowVisible(StatusBarHandle))
            ShowWindow(StatusBarHandle, SW_HIDE);
    }
}

VOID LoadToolbarSettings(
    VOID
    )
{
    RebarLoadSettings();

    if (EnableToolBar && ToolBarHandle)
    {
        ULONG index = 0;
        ULONG buttonCount = 0;

        buttonCount = (ULONG)SendMessage(ToolBarHandle, TB_BUTTONCOUNT, 0, 0);

        for (index = 0; index < buttonCount; index++)
        {
            TBBUTTONINFO button = { sizeof(TBBUTTONINFO) };
            button.dwMask = TBIF_BYINDEX | TBIF_STYLE | TBIF_COMMAND | TBIF_TEXT | TBIF_STATE;

            // Get settings for first button
            if (SendMessage(ToolBarHandle, TB_GETBUTTONINFO, index, (LPARAM)&button) == -1)
                break;

            // Skip separator buttons
            if (button.fsStyle == BTNS_SEP)
                continue;

            // TODO: We manually add the text above using TB_ADDSTRING,
            //       why do we need to set the button text again when changing TBIF_STYLE?
            switch (button.idCommand)
            {
            case PHAPP_ID_VIEW_REFRESH:
                button.pszText = L"Refresh";
                break;
            case PHAPP_ID_HACKER_OPTIONS:
                button.pszText = L"Options";
                break;
            case PHAPP_ID_HACKER_FINDHANDLESORDLLS:
                button.pszText = L"Find Handles or DLLs";
                break;
            case PHAPP_ID_VIEW_SYSTEMINFORMATION:
                button.pszText = L"System Information";
                break;
            case TIDC_FINDWINDOW:
                button.pszText = L"Find Window";
                break;
            case TIDC_FINDWINDOWTHREAD:
                button.pszText = L"Find Window and Thread";
                break;
            case TIDC_FINDWINDOWKILL:
                button.pszText = L"Find Window and Kill";
                break;
            case PHAPP_ID_VIEW_ALWAYSONTOP:
                button.pszText = L"Always on Top";
                break;
            }

            if (button.idCommand == PHAPP_ID_VIEW_ALWAYSONTOP)
            {
                BOOLEAN isAlwaysOnTopEnabled = (BOOLEAN)PhGetIntegerSetting(L"MainWindowAlwaysOnTop");

                // Set the pressed state
                if (isAlwaysOnTopEnabled)  
                {
                    button.fsState |= TBSTATE_PRESSED;
                }
            }

            switch (DisplayStyle)
            {
            case ToolbarDisplayImageOnly:
                button.fsStyle = BTNS_BUTTON | BTNS_AUTOSIZE;
                break;
            case ToolbarDisplaySelectiveText:
                {
                    switch (button.idCommand)
                    {
                    case PHAPP_ID_VIEW_REFRESH:
                    case PHAPP_ID_HACKER_OPTIONS:
                    case PHAPP_ID_HACKER_FINDHANDLESORDLLS:
                    case PHAPP_ID_VIEW_SYSTEMINFORMATION:
                        button.fsStyle = BTNS_BUTTON | BTNS_AUTOSIZE | BTNS_SHOWTEXT;
                        break;
                    default:
                        button.fsStyle = BTNS_BUTTON | BTNS_AUTOSIZE;
                        break;
                    }
                }
                break;
            case ToolbarDisplayAllText:
                button.fsStyle = BTNS_BUTTON | BTNS_AUTOSIZE | BTNS_SHOWTEXT;
                break;
            }

            // Set updated button info
            SendMessage(ToolBarHandle, TB_SETBUTTONINFO, index, (LPARAM)&button);
        }
       
        // Resize the toolbar
        SendMessage(ToolBarHandle, TB_AUTOSIZE, 0, 0);    
        //InvalidateRect(ToolBarHandle, NULL, TRUE);
    }

    // Invoke the LayoutPaddingCallback.
    SendMessage(PhMainWndHandle, WM_SIZE, 0, 0);
}

VOID ResetToolbarSettings(
    VOID
    )
{
    // Remove all the user customizations.
    INT buttonCount = (INT)SendMessage(ToolBarHandle, TB_BUTTONCOUNT, 0, 0);
    while (buttonCount--)
        SendMessage(ToolBarHandle, TB_DELETEBUTTON, (WPARAM)buttonCount, 0);

    // Re-add the original buttons.
    SendMessage(ToolBarHandle, TB_ADDBUTTONS, MAX_DEFAULT_TOOLBAR_ITEMS, (LPARAM)ToolbarButtons);

}