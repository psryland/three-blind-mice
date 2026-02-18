import { useState, useCallback, useEffect } from 'react';
import RoomPanel from './components/RoomPanel';
import './App.css';

const STORAGE_KEY_NAME = 'tbm_user_name';

function App() {
	const [room_code, set_room_code] = useState<string | null>(null);
	const [is_host, set_is_host] = useState(false);
	const [user_name, set_user_name] = useState(() => {
		return localStorage.getItem(STORAGE_KEY_NAME) ?? '';
	});

	// Persist name to localStorage when it changes
	useEffect(() => {
		localStorage.setItem(STORAGE_KEY_NAME, user_name);
	}, [user_name]);

	const handle_join = useCallback((code: string, host: boolean) => {
		set_room_code(code);
		set_is_host(host);
	}, []);

	const handle_leave = useCallback(() => {
		set_room_code(null);
		set_is_host(false);
	}, []);

	return (
		<div className="app">
			<h1>Three Blind Mice</h1>

			<div className="name-input">
				<label htmlFor="user-name">Your Name: </label>
				<input
					id="user-name"
					type="text"
					placeholder="Enter your name"
					value={user_name}
					onChange={(e) => set_user_name(e.target.value)}
					maxLength={32}
				/>
			</div>

			<RoomPanel
				on_join={handle_join}
				on_leave={handle_leave}
				room_code={room_code}
			/>

			{room_code && (
				<div className="canvas-placeholder">
					<p>
						Connected to room <strong>{room_code}</strong>
						{is_host ? ' (host)' : ' (remote)'}
						{user_name ? ` as ${user_name}` : ''}
					</p>
					<p className="placeholder-hint">Mouse canvas will render here</p>
				</div>
			)}
		</div>
	);
}

export default App;
