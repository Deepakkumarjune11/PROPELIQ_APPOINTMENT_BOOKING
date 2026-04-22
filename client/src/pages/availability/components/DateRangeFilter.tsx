// Controlled date-range picker for the availability search filter.
// UXR-101: keyboard accessible; UXR-102: min-height 44px touch target on the search button.
import SearchIcon from '@mui/icons-material/Search';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Grid from '@mui/material/Grid';
import TextField from '@mui/material/TextField';

interface DateRangeFilterProps {
  startDate: string;
  endDate: string;
  onStartDateChange: (value: string) => void;
  onEndDateChange: (value: string) => void;
  onSearch: () => void;
  isSearching: boolean;
  /** Inline validation message — displayed on the end-date field. */
  validationError: string;
}

export default function DateRangeFilter({
  startDate,
  endDate,
  onStartDateChange,
  onEndDateChange,
  onSearch,
  isSearching,
  validationError,
}: DateRangeFilterProps) {
  const today = new Date().toISOString().split('T')[0];

  return (
    <Box
      component="section"
      aria-label="Search filters"
      sx={{
        bgcolor: 'background.paper',
        p: 3,
        borderRadius: 2,
        boxShadow: 1,
        mb: 4,
      }}
    >
      <Grid container spacing={2} alignItems="flex-start">
        <Grid item xs={12} sm={6} md={4}>
          <TextField
            label="Start date"
            type="date"
            value={startDate}
            onChange={(e) => onStartDateChange(e.target.value)}
            fullWidth
            InputLabelProps={{ shrink: true }}
            inputProps={{
              'aria-label': 'Start date',
              min: today,
            }}
          />
        </Grid>

        <Grid item xs={12} sm={6} md={4}>
          <TextField
            label="End date"
            type="date"
            value={endDate}
            onChange={(e) => onEndDateChange(e.target.value)}
            fullWidth
            InputLabelProps={{ shrink: true }}
            inputProps={{
              'aria-label': 'End date',
              min: startDate || today,
            }}
            error={Boolean(validationError)}
            helperText={validationError || undefined}
          />
        </Grid>

        <Grid item xs={12} sm={12} md={4}>
          {/* Label spacer keeps button vertically aligned with inputs (no label row above it). */}
          <Button
            variant="contained"
            color="primary"
            startIcon={<SearchIcon />}
            onClick={onSearch}
            disabled={isSearching || Boolean(validationError)}
            fullWidth
            sx={{ minHeight: 56, mt: { xs: 0, md: '0px' } }}
            aria-label="Search available slots"
          >
            {isSearching ? 'Searching…' : 'Search availability'}
          </Button>
        </Grid>
      </Grid>
    </Box>
  );
}
