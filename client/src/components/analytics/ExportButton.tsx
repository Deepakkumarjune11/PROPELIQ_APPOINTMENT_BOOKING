// Export button with PDF/CSV menu for analytics dashboard (US_033, AC-5, SCR-028).
import DownloadIcon from '@mui/icons-material/Download';
import {
  Button,
  CircularProgress,
  Menu,
  MenuItem,
} from '@mui/material';
import { useState } from 'react';

import { downloadExport } from '@/api/analytics';
import type { DateRange } from '@/types/analytics';

interface Props {
  range: DateRange;
}

export default function ExportButton({ range }: Props) {
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const [downloading, setDownloading] = useState(false);

  const open = Boolean(anchorEl);

  const handleButtonClick = (e: React.MouseEvent<HTMLButtonElement>) => {
    setAnchorEl(e.currentTarget);
  };

  const handleClose = () => {
    setAnchorEl(null);
  };

  const handleExport = async (format: 'pdf' | 'csv') => {
    handleClose();
    setDownloading(true);
    try {
      await downloadExport(format, range);
    } finally {
      setDownloading(false);
    }
  };

  return (
    <>
      <Button
        variant="outlined"
        startIcon={downloading ? <CircularProgress size={16} /> : <DownloadIcon />}
        onClick={handleButtonClick}
        disabled={downloading}
        aria-haspopup="true"
        aria-expanded={open ? 'true' : undefined}
        aria-controls={open ? 'export-menu' : undefined}
      >
        Export
      </Button>
      <Menu
        id="export-menu"
        anchorEl={anchorEl}
        open={open}
        onClose={handleClose}
        MenuListProps={{ 'aria-label': 'Export format options' }}
      >
        <MenuItem onClick={() => void handleExport('csv')}>Export CSV</MenuItem>
        <MenuItem onClick={() => void handleExport('pdf')}>Export PDF</MenuItem>
      </Menu>
    </>
  );
}
