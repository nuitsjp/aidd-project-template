//go:build windows && !server

package main

import (
	"syscall"
	"unsafe"
)

func showStartupFailure() {
	title, _ := syscall.UTF16PtrFromString("Wailsアプリを起動できません")                                                                              //nolint:errcheck // This fixed literal cannot contain a NUL.
	message, _ := syscall.UTF16PtrFromString("初期化に失敗しました。設定・保存データ・アクセス権を確認してください。保存データを空の内容で上書きしていません。")                                    //nolint:errcheck // This fixed literal cannot contain a NUL.
	syscall.NewLazyDLL("user32.dll").NewProc("MessageBoxW").Call(0, uintptr(unsafe.Pointer(message)), uintptr(unsafe.Pointer(title)), 0x10) //nolint:errcheck // Failure to display the terminal startup error has no recovery path; stderr already reports it.
}
