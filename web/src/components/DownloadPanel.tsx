import { useState } from 'react';
import './DownloadPanel.css';

interface DownloadInfo {
	platform: string;
	label: string;
	filename: string;
}

const DOWNLOADS: DownloadInfo[] = [
	{
		platform: 'win',
		label: 'Windows (x64)',
		filename: 'three-blind-mice-win-x64.zip',
	},
	{
		platform: 'linux',
		label: 'Linux (x64)',
		filename: 'three-blind-mice-linux-x64.zip',
	},
];

function Detect_Platform(): string {
	const ua = navigator.userAgent.toLowerCase();
	if (ua.includes('win')) return 'win';
	if (ua.includes('linux')) return 'linux';
	return 'win';
}

export default function DownloadPanel() {
	const [detected_platform] = useState(Detect_Platform);
	const [show_all, set_show_all] = useState(false);

	const primary = DOWNLOADS.find((d) => d.platform === detected_platform) ?? DOWNLOADS[0];
	const others = DOWNLOADS.filter((d) => d.platform !== detected_platform);

	return (
		<div className="download-panel-inline">
			<p className="download-inline-hint">
				Don't have the host app? Download it below.
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
		</div>
	);
}
