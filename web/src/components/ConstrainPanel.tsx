import { useState } from 'react';
import './ConstrainPanel.css';

type ConstrainMode = 'monitors' | 'window' | 'rectangle';

export default function ConstrainPanel() {
	const [mode, set_mode] = useState<ConstrainMode>('monitors');

	return (
		<div className="constrain-panel">
			<h3>Constrain Mouse to:</h3>
			<div className="constrain-panel-options">
				<div className="constrain-panel-option">
					<input
						type="radio"
						id="constrain-monitors"
						name="constrain_mode"
						value="monitors"
						checked={mode === 'monitors'}
						onChange={() => set_mode('monitors')}
					/>
					<label htmlFor="constrain-monitors">Monitors</label>
				</div>
				<div className="constrain-panel-option">
					<input
						type="radio"
						id="constrain-window"
						name="constrain_mode"
						value="window"
						checked={mode === 'window'}
						onChange={() => set_mode('window')}
					/>
					<label htmlFor="constrain-window">Specific Window</label>
				</div>
				<div className="constrain-panel-option">
					<input
						type="radio"
						id="constrain-rectangle"
						name="constrain_mode"
						value="rectangle"
						checked={mode === 'rectangle'}
						onChange={() => set_mode('rectangle')}
					/>
					<label htmlFor="constrain-rectangle">Rectangle</label>
				</div>
			</div>
		</div>
	);
}
