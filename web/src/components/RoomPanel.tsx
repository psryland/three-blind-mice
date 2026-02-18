import { useState, useEffect, useCallback } from 'react';
import { Generate_Room_Code } from '../types';
import './RoomPanel.css';

interface RoomPanelProps {
	on_join: (code: string, is_host: boolean) => void;
	on_leave: () => void;
	room_code: string | null;
}

export default function RoomPanel({ on_join, on_leave, room_code }: RoomPanelProps) {
	const [join_input, set_join_input] = useState('');
	const [copy_label, set_copy_label] = useState('Copy Link');

	// Auto-join from ?room= query parameter on mount
	useEffect(() => {
		const params = new URLSearchParams(window.location.search);
		const room_param = params.get('room');
		if (room_param && !room_code) {
			on_join(room_param.toLowerCase(), false);
		}
	}, []); // eslint-disable-line react-hooks/exhaustive-deps

	const handle_create = useCallback(() => {
		const code = Generate_Room_Code();
		on_join(code, true);
	}, [on_join]);

	const handle_join = useCallback(() => {
		const code = join_input.trim().toLowerCase();
		if (code.length >= 4) {
			on_join(code, false);
			set_join_input('');
		}
	}, [join_input, on_join]);

	const shareable_url = `https://three-blind-mice.rylogic.co.nz?room=${room_code}`;

	const handle_copy_link = useCallback(async () => {
		try {
			await navigator.clipboard.writeText(shareable_url);
			set_copy_label('Copied!');
			setTimeout(() => set_copy_label('Copy Link'), 2000);
		} catch {
			set_copy_label('Failed');
			setTimeout(() => set_copy_label('Copy Link'), 2000);
		}
	}, [shareable_url]);

	// Active room view
	if (room_code) {
		return (
			<div className="room-panel">
				<div className="room-active">
					<h2>Room: <span className="room-code">{room_code}</span></h2>
					<div className="shareable-url">
						<input type="text" readOnly value={shareable_url} />
						<button className="btn-primary" onClick={handle_copy_link}>{copy_label}</button>
					</div>
					<button className="leave-btn" onClick={on_leave}>Leave Room</button>
				</div>
			</div>
		);
	}

	// Lobby view
	return (
		<div className="room-panel">
			<div className="room-section">
				<h3>Create Room</h3>
				<button className="btn-primary" onClick={handle_create}>Create New Room</button>
			</div>

			<div className="room-divider">or</div>

			<div className="room-section">
				<h3>Join Room</h3>
				<div className="join-controls">
					<input
						type="text"
						placeholder="Enter room code"
						value={join_input}
						onChange={(e) => set_join_input(e.target.value)}
						onKeyDown={(e) => e.key === 'Enter' && handle_join()}
						maxLength={8}
					/>
					<button onClick={handle_join} disabled={join_input.trim().length < 4}>
						Join
					</button>
				</div>
			</div>
		</div>
	);
}
