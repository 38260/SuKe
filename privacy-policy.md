# Privacy Policy

## Overview

SuKe Browser Connector is a browser extension designed to save and restore the user’s browser tabs and tab groups as part of a SuKe workspace session.

This extension is intended to help users preserve their browser context when switching between desktop workspaces. It does not collect, sell, or share personal data with third parties.

## What data the extension uses

The extension may read the user’s currently open tabs and tab group information so that the user can save and restore their browser state with their SuKe workspace.

This may include:
- tab titles
- tab URLs
- tab group information
- browser-window bounds and session state needed to restore the current workflow
- an opaque random connector-profile key used to keep tab reuse inside the capturing profile

This data is used only to:
- save the current browser state
- restore the browser state later
- support the user’s local SuKe workflow

## Where the data goes

The extension communicates with the user’s local SuKe desktop application through native messaging.

The data is used locally on the user’s device and may be retained in the local SuKe
workspace the user saves. It is not sent to a remote server by the connector for tracking,
advertising, analytics, or any unrelated purpose. Data leaves the device only when the user
explicitly exports it or configures a separate SuKe synchronization destination.

Mozilla classifies native-messaging transfer as transmission outside the add-on. The Firefox
package therefore declares browsing activity, website content, and personally identifying
information for URLs, titles/group labels, and the opaque profile key, even though the connector
does not send that data off the user's PC.

## Desktop screen previews

When the SuKe desktop app saves a named workspace, it captures one screenshot of each
connected monitor. These images may show any visible content, including private browser windows,
password managers, notifications, and windows excluded from workspace entries. The browser
connector's private-window exclusion does not apply to desktop screenshots.

The screenshots are stored locally under `%AppData%\SuKe\previews`. They are not included
in workspace import/export files or diagnostic exports. Deleting a saved workspace also deletes its
screen preview. A failed screenshot does not prevent the workspace from being saved; the app warns
when the screen preview is incomplete.

## No remote code

This extension does not use remote code execution. It does not download or execute code from a remote source. It runs locally in the browser and communicates with the local SuKe application installed on the user’s device.

## No data collection for advertising or analytics

This extension does not:
- request browser account identity, a profile name, or a device identifier
- track browsing activity for advertising purposes
- sell user data
- share data with third parties
- use analytics or advertisement networks
- use location tracking, account data, or payment data

Private/incognito windows are excluded. The connectors do not request cookies, passwords, general
browsing history, page bodies, form data, downloads, bookmarks, or Firefox container identity.

## Data retention

The extension stores browser session data only as needed for the user’s local SuKe workspace save and restore functionality. Data is not retained for marketing, profiling, or unrelated business purposes.

## Changes to this policy

This privacy policy may be updated from time to time. Continued use of the extension after an update means the user accepts the revised policy.

## Contact

For privacy questions or support, contact the publisher through the store listing or the SuKe repository.
