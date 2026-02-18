import { useState, useEffect, useCallback } from 'react';
import { Generate_Session_Code } from '../types';
import './SessionPanel.css';

interface SessionPanelProps {
	on_join: (code: string, is_host: boolean) => void;
	on_leave: () => void;
	session_code: string | null;
}

export default function SessionPanel({ on_join, on_leave, session_code }: SessionPanelProps) {
	const [join_input, set_join_input] = useState('');
	const [copy_label, set_copy_label] = useState('Copy Link');

	// Auto-join from ?session= query parameter on mount
	useEffect(() => {
		const params = new URLSearchParams(window.location.search);
		const session_param = params.get('session');
		if (session_param && !session_code) {
			on_join(session_param.toLowerCase(), false);
		}
	}, []); // eslint-disable-line react-hooks/exhaustive-deps

	const handle_create = useCallback(() => {
		const code = Generate_Session_Code();
		on_join(code, true);
	}, [on_join]);

	const handle_join = useCallback(() => {
		const code = join_input.trim().toLowerCase();
		if (code.length >= 4) {
			on_join(code, false);
			set_join_input('');
		}
	}, [join_input, on_join]);

	const shareable_url = `https://three-blind-mice.rylogic.co.nz?session=${session_code}`;

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

	// Active session view
	if (session_code) {
		return (
			<div className="session-panel">
				<div className="session-active">
					<h2>Session: <span className="session-code">{session_code}</span></h2>
					<div className="shareable-url">
						<input type="text" readOnly value={shareable_url} />
						<button className="btn-primary" onClick={handle_copy_link}>{copy_label}</button>
					</div>
					<button className="leave-btn" onClick={on_leave}>Leave Session</button>
				</div>
			</div>
		);
	}

	// Lobby view
	return (
		<div className="session-panel">
			<div className="session-section">
				<h3>Start Session</h3>
				<button className="btn-primary" onClick={handle_create}>Start New Session</button>
			</div>

			<div className="session-divider">or</div>

			<div className="session-section">
				<h3>Join Session</h3>
				<div className="join-controls">
					<input
						type="text"
						placeholder="Enter session code"
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
