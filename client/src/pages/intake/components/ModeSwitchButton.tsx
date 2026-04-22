// "Switch to conversational" mode button — navigates to SCR-005 without clearing answers (AC-2).
import SwapHorizIcon from '@mui/icons-material/SwapHoriz';
import Button from '@mui/material/Button';
import { useNavigate } from 'react-router-dom';

import { useIntakeStore } from '@/stores/intake-store';

export default function ModeSwitchButton() {
  const navigate = useNavigate();
  const { setMode } = useIntakeStore();

  const handleSwitch = () => {
    // AC-2: mode is updated but answers are deliberately NOT cleared
    setMode('conversational');
    navigate('/appointments/intake/conversational');
  };

  return (
    <Button
      variant="outlined"
      size="small"
      startIcon={<SwapHorizIcon />}
      onClick={handleSwitch}
      sx={{ minHeight: 44, whiteSpace: 'nowrap' }}
      aria-label="Switch to conversational intake mode"
    >
      Switch to conversational
    </Button>
  );
}
