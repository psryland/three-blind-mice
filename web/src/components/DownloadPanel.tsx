import { useState, useEffect } from 'react';
import './DownloadPanel.css';

interface DownloadInfo {
	platform: string;
	label: string;
	filename: string;
	sha256: string;
}

const DOWNLOADS: DownloadInfo[] = [
	{
		platform: 'win',
		label: 'Windows (x64)',
		filename: 'three-blind-mice-win-x64.zip',
		sha256: '9C523BDAECFA4BAD80BE6404165B7ED1C2F9D30D0582B2D9A4154F1C913CC47C',
	},
	{
		platform: 'linux',
		label: 'Linux (x64)',
		filename: 'three-blind-mice-linux-x64.zip',
		sha256: '5A62001BD976839C16E448959E3DD7818D62E8F2A63009A78F1919CCC3A016F0',
	},
];

function Detect_Platform(): string {
	const ua = navigator.userAgent.toLowerCase();
	if (ua.includes('win')) return 'win';
	if (ua.includes('linux')) return 'linux';
	return 'win';
}

interface Props {
	room_code: string | null;
	is_host: boolean;
}

export default function DownloadPanel({ room_code, is_host }: Props) {
	const [detected_platform] = useState(Detect_Platform);
	const [show_all, set_show_all] = useState(false);
	const [launch_attempted, set_launch_attempted] = useState(false);

	const primary = DOWNLOADS.find((d) => d.platform === detected_platform) ?? DOWNLOADS[0];
	const others = DOWNLOADS.filter((d) => d.platform !== detected_platform);

	// Reset launch state when room changes
	useEffect(() => {
		set_launch_attempted(false);
	}, [room_code]);

	const handle_launch = () => {
		if (!room_code) return;
		set_launch_attempted(true);

		// Attempt to launch via tbm:// protocol
		window.location.href = `tbm://room/${room_code}`;
	};

	return (
		<div className="download-panel">
			<h3>Host App</h3>
			<p className="download-description">
				Download the overlay app to display remote cursors on your desktop.
				Requires <a href="https://dotnet.microsoft.com/download/dotnet/8.0" target="_blank" rel="noopener noreferrer">.NET 8 Runtime</a>.
			</p>

			<div className="download-buttons">
				<a
					href={`/downloads/${primary.filename}`}
					className="download-btn primary"
					download
				>
					⬇ {primary.label}
				</a>

				{!show_all && others.length > 0 && (
					<button className="download-btn secondary" onClick={() => set_show_all(true)}>
						Other platforms
					</button>
				)}

				{show_all && others.map((d) => (
					<a
						key={d.platform}
						href={`/downloads/${d.filename}`}
						className="download-btn secondary"
						download
					>
						⬇ {d.label}
					</a>
				))}
			</div>

			{room_code && is_host && (
				<div className="launch-section">
					<button className="launch-btn" onClick={handle_launch}>
						🚀 Launch Overlay
					</button>
					{launch_attempted && (
						<p className="launch-hint">
							If the overlay didn't open, download and run it first. The app registers
							the <code>tbm://</code> protocol on first run.
						</p>
					)}
				</div>
			)}

			<details className="hash-details">
				<summary>Verify download (SHA256)</summary>
				{DOWNLOADS.map((d) => (
					<div key={d.platform} className="hash-entry">
						<span className="hash-label">{d.label}:</span>
						<code className="hash-value">{d.sha256}</code>
					</div>
				))}
			</details>
		</div>
	);
}
