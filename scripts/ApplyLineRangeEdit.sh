#!/usr/bin/env bash
set -euo pipefail

if ! command -v perl >/dev/null 2>&1; then
    printf '%s\n' 'ApplyLineRangeEdit.sh requires Perl 5 with the core JSON::PP and Encode modules.' >&2
    exit 1
fi

exec perl - "$@" <<'PERL'
use strict;
use warnings;
use Cwd qw(getcwd);
use Encode qw(decode encode FB_CROAK);
use File::Spec;
use JSON::PP qw(decode_json encode_json);
use Fcntl qw(:DEFAULT);
use Scalar::Util qw(looks_like_number);

my $utf8_bom = "\xEF\xBB\xBF";

sub parse_arguments {
    my (@arguments) = @_;
    my %options;
    for (my $index = 0; $index < scalar @arguments; $index++) {
        my $argument = $arguments[$index];
        next if $argument eq '--';
        die "Expected an option followed by a value, got '$argument'.\n"
            if $argument !~ /^--/ || $index + 1 >= scalar @arguments;
        my $name = substr($argument, 2);
        my $value = $arguments[++$index];
        my $normalized_name = lc $name;
        die "Invalid or duplicated option '--$name'.\n"
            if $name eq '' || $value =~ /^--/ || exists $options{$normalized_name};
        $options{$normalized_name} = $value;
    }
    return \%options;
}

sub require_path {
    my ($options, $name) = @_;
    my $value = $options->{$name};
    die "Missing required option '--$name'.\n" if !defined $value || $value =~ /^\s*$/;
    return File::Spec->rel2abs($value, getcwd());
}

sub has_utf8_bom {
    my ($bytes) = @_;
    return length($bytes) >= 3 && substr($bytes, 0, 3) eq $utf8_bom;
}

sub read_bytes {
    my ($file_path) = @_;
    open my $handle, '<:raw', $file_path or die "Unable to read '$file_path': $!\n";
    local $/;
    my $bytes = <$handle>;
    close $handle or die "Unable to close '$file_path': $!\n";
    return defined $bytes ? $bytes : '';
}

sub decode_utf8 {
    my ($bytes, $file_path) = @_;
    $bytes = substr($bytes, 3) if has_utf8_bom($bytes);
    my $text;
    eval { $text = decode('UTF-8', $bytes, FB_CROAK); 1 }
        or die "File '$file_path' is not valid UTF-8: $@";
    return $text;
}

sub read_utf8_text {
    my ($file_path) = @_;
    return decode_utf8(read_bytes($file_path), $file_path);
}

sub normalize {
    my ($text) = @_;
    $text =~ s/\r\n/\n/g;
    $text =~ s/\r/\n/g;
    return $text;
}

sub split_lines {
    my ($text) = @_;
    my $normalized = normalize($text);
    return () if $normalized eq '';
    chop $normalized if substr($normalized, -1) eq "\n";
    return ('') if $normalized eq '';
    return split /\n/, $normalized, -1;
}

sub detect_newline {
    my ($text) = @_;
    return "\r\n" if index($text, "\r\n") >= 0;
    return "\r" if index($text, "\r") >= 0;
    return "\n";
}

sub trailing_newline {
    my ($text) = @_;
    return "\r\n" if $text =~ /\r\n\z/;
    return "\n" if $text =~ /\n\z/;
    return "\r" if $text =~ /\r\z/;
    return '';
}

sub convert_newlines {
    my ($text, $newline) = @_;
    $text =~ s/\n/$newline/g if $newline ne "\n";
    return $text;
}

sub boundary_equals {
    my ($actual, $expected) = @_;
    $actual =~ s/^\s+|\s+$//g;
    $expected =~ s/^\s+|\s+$//g;
    return lc($actual) eq lc($expected);
}

sub required_positive_integer {
    my ($edit, $property_name) = @_;
    my $value = ref($edit) eq 'HASH' ? $edit->{$property_name} : undef;
    my $encoded = defined $value && !ref($value) ? encode_json($value) : '';
    die "Edit property '$property_name' must be a positive integer.\n"
        if !defined $value || ref($value) || $encoded =~ /^\"/ || !looks_like_number($value) || $value !~ /^\d+$/ || $value < 1;
    return int($value);
}

sub required_path {
    my ($edit, $property_name) = @_;
    my $value = ref($edit) eq 'HASH' ? $edit->{$property_name} : undef;
    die "Edit property '$property_name' must be a non-empty path.\n"
        if !defined $value || ref($value) || $value =~ /^\s*$/;
    if ($value =~ /^([A-Za-z]):[\\\/](.*)$/) {
        my $drive = lc $1;
        my $rest = $2;
        $rest =~ s#\\#/#g;
        return "/mnt/$drive/$rest";
    }
    return File::Spec->rel2abs($value, getcwd());
}

sub read_boundary {
    my ($file_path) = @_;
    my $content = read_utf8_text($file_path);
    my @lines = split_lines($content);
    return '' if scalar @lines == 0 && normalize($content) eq '';
    die "Boundary file '$file_path' must contain exactly one logical line.\n" if scalar @lines != 1;
    return $lines[0];
}

sub read_edits {
    my ($manifest_path) = @_;
    my $manifest_text = read_utf8_text($manifest_path);
    my $manifest = eval { decode_json($manifest_text) };
    die "Unable to read edits manifest '$manifest_path': $@" if !$manifest;
    die "The edits manifest must contain a non-empty 'edits' array.\n"
        if ref($manifest) ne 'HASH' || ref($manifest->{edits}) ne 'ARRAY' || !@{$manifest->{edits}};

    my @edits;
    for my $edit (@{$manifest->{edits}}) {
        my $start_line = required_positive_integer($edit, 'start_line');
        my $end_line = required_positive_integer($edit, 'end_line');
        die "Invalid edit range $start_line-$end_line.\n" if $end_line < $start_line;
        my $expected_start_path = required_path($edit, 'expected_start_file');
        my $expected_end_path = required_path($edit, 'expected_end_file');
        my $replacement_path = required_path($edit, 'replacement_file');
        push @edits, {
            start_line => $start_line,
            end_line => $end_line,
            expected_start => read_boundary($expected_start_path),
            expected_end => read_boundary($expected_end_path),
            replacement_lines => [split_lines(read_utf8_text($replacement_path))],
        };
    }
    return \@edits;
}

sub validate_ranges {
    my ($edits, $original_lines, $target_path) = @_;
    my @ordered = sort { $a->{start_line} <=> $b->{start_line} } @{$edits};
    for (my $index = 0; $index < scalar @ordered; $index++) {
        my $edit = $ordered[$index];
        die "Edit range $edit->{start_line}-$edit->{end_line} exceeds '$target_path' ("
            . scalar(@{$original_lines}) . " lines).\n"
            if $edit->{start_line} > scalar(@{$original_lines}) || $edit->{end_line} > scalar(@{$original_lines});
        die "Edit range $edit->{start_line}-$edit->{end_line} has mismatched boundary content.\n"
            if !boundary_equals($original_lines->[$edit->{start_line} - 1], $edit->{expected_start})
            || !boundary_equals($original_lines->[$edit->{end_line} - 1], $edit->{expected_end});
        if ($index > 0 && $ordered[$index - 1]->{end_line} >= $edit->{start_line}) {
            die "Edit ranges $ordered[$index - 1]->{start_line}-$ordered[$index - 1]->{end_line} and "
                . "$edit->{start_line}-$edit->{end_line} overlap.\n";
        }
    }
}

sub same_lines {
    my ($actual, $expected) = @_;
    return 0 if scalar @{$actual} != scalar @{$expected};
    for (my $index = 0; $index < scalar @{$expected}; $index++) {
        return 0 if $actual->[$index] ne $expected->[$index];
    }
    return 1;
}

sub validate_result {
    my ($file_path, $expected_lines, $expected_trailing, $expected_bom) = @_;
    my $bytes = read_bytes($file_path);
    my $text = decode_utf8($bytes, $file_path);
    die "Post-write validation failed for '$file_path'.\n"
        if !same_lines([split_lines($text)], $expected_lines)
        || trailing_newline($text) ne $expected_trailing
        || has_utf8_bom($bytes) != $expected_bom;
}

sub write_utf8_file {
    my ($file_path, $text, $with_bom) = @_;
    my $bytes = encode('UTF-8', $text, FB_CROAK);
    $bytes = $utf8_bom . $bytes if $with_bom;
    sysopen my $handle, $file_path, O_WRONLY | O_CREAT | O_EXCL or die "Unable to create '$file_path': $!\n";
    binmode $handle;
    print {$handle} $bytes or die "Unable to write '$file_path': $!\n";
    close $handle or die "Unable to close '$file_path': $!\n";
}

sub main {
    my $options = parse_arguments(@ARGV);
    my $target_path = require_path($options, 'target-file');
    my $edits_manifest_path = require_path($options, 'edits-file');
    my $original_bytes = read_bytes($target_path);
    my $original = decode_utf8($original_bytes, $target_path);
    my $newline = detect_newline($original);
    my $original_trailing = trailing_newline($original);
    my @original_lines = split_lines($original);
    my $edits = read_edits($edits_manifest_path);
    validate_ranges($edits, \@original_lines, $target_path);

    my @expected_lines = @original_lines;
    my @descending = sort { $b->{start_line} <=> $a->{start_line} } @{$edits};
    for my $edit (@descending) {
        splice @expected_lines, $edit->{start_line} - 1, $edit->{end_line} - $edit->{start_line} + 1,
            @{$edit->{replacement_lines}};
    }

    my $updated = convert_newlines(join("\n", @expected_lines), $newline) . $original_trailing;
    my $temporary_path = $target_path . '.' . time() . '.' . $$ . '.range-edit.tmp';
    $temporary_path .= '.' . $$ if -e $temporary_path;
    eval {
        write_utf8_file($temporary_path, $updated, has_utf8_bom($original_bytes));
        validate_result($temporary_path, \@expected_lines, $original_trailing, has_utf8_bom($original_bytes));
        rename($temporary_path, $target_path) or die "Unable to replace '$target_path': $!\n";
        validate_result($target_path, \@expected_lines, $original_trailing, has_utf8_bom($original_bytes));
        print "Applied " . scalar(@{$edits}) . " line-range edits to '$target_path' from one original read.\n";
        1;
    } or do {
        my $error = $@;
        unlink $temporary_path if -e $temporary_path;
        die $error;
    };
}

eval { main(); 1 } or do {
    my $error = $@;
    chomp $error;
    print STDERR "$error\n";
    exit 1;
};
PERL
