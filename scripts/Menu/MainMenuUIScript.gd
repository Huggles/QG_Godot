extends Control

signal host_button_clicked()
signal join_button_clicked()
signal exit_button_clicked()

func _on_host_button_pressed():
	host_button_clicked.emit()

func _on_multiplayer_button_pressed():
	join_button_clicked.emit()

func _on_exit_game_button_pressed():
	exit_button_clicked.emit()


func _on_debug_pressed():
	MultiplayerManager._debug()
	pass # Replace with function body.
