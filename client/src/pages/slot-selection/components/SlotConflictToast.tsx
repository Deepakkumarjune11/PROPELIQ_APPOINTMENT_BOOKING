// UXR-404: MUI Snackbar toast for 409 Conflict — slot no longer available.
// Position: bottom-center. Auto-hides after 5 s. severity="error".
import Alert from '@mui/material/Alert';
import Snackbar from '@mui/material/Snackbar';

const AUTO_HIDE_MS = 5000;

interface SlotConflictToastProps {
  open: boolean;
  onClose: () => void;
}

export default function SlotConflictToast({ open, onClose }: SlotConflictToastProps) {
  return (
    <Snackbar
      open={open}
      autoHideDuration={AUTO_HIDE_MS}
      onClose={onClose}
      anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
    >
      {/* Alert must be a direct child of Snackbar for correct role="alert" propagation */}
      <Alert
        onClose={onClose}
        severity="error"
        variant="filled"
        sx={{ width: '100%' }}
      >
        Slot no longer available. Please select another.
      </Alert>
    </Snackbar>
  );
}
