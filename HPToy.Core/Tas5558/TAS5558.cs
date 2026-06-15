namespace HPToy.Core.Tas5558;

public static class TAS5558
{
    public const byte I2C_ADDR = 0x34;
    public const int TAS5558_FS = 96000;

    public const byte CLOCK_CONTROL_REG = 0x00;
    public const byte GENERAL_STATUS_REG = 0x01;
    public const byte ERROR_STATUS_REG = 0x02;
    public const byte SYSTEM_CONTROL1_REG = 0x03;
    public const byte SYSTEM_CONTROL2_REG = 0x04;

    public const byte CH1_CONFIG_CONTROL_REG = 0x05;
    public const byte CH2_CONFIG_CONTROL_REG = 0x06;
    public const byte CH3_CONFIG_CONTROL_REG = 0x07;
    public const byte CH4_CONFIG_CONTROL_REG = 0x08;
    public const byte CH5_CONFIG_CONTROL_REG = 0x09;
    public const byte CH6_CONFIG_CONTROL_REG = 0x0A;
    public const byte CH7_CONFIG_CONTROL_REG = 0x0B;
    public const byte CH8_CONFIG_CONTROL_REG = 0x0C;

    public const byte HEADPHONE_CONFIG_CONTROL_REG = 0x0D;
    public const byte SERIAL_DATA_INTERFACE_CONTROL_REG = 0x0E;
    public const byte SOFT_MUTE_REG = 0x0F;
    public const byte ENERGY_MANAGERS_REG = 0x10;
    public const byte RESERVED0 = 0x11;
    public const byte OSCILLATOR_TRIM = 0x12;
    public const byte RESERVED = 0x13;
    public const byte AUTOMUTE_CONTROL1_REG = 0x14;
    public const byte AUTOMUTE_CONTROL2_REG = 0x15;

    public const byte MODULATION12_LIMIT_REG = 0x16;
    public const byte MODULATION34_LIMIT_REG = 0x17;
    public const byte MODULATION56_LIMIT_REG = 0x18;
    public const byte MODULATION78_LIMIT_REG = 0x19;
    public const byte RESERVED1 = 0x1A;

    public const byte DELAY_CH1_REG = 0x1B;
    public const byte DELAY_CH2_REG = 0x1C;
    public const byte DELAY_CH3_REG = 0x1D;
    public const byte DELAY_CH4_REG = 0x1E;
    public const byte DELAY_CH5_REG = 0x1F;
    public const byte DELAY_CH6_REG = 0x20;
    public const byte DELAY_CH7_REG = 0x21;
    public const byte DELAY_CH8_REG = 0x22;

    public const byte OFFSET_DELAY_REG = 0x23;
    public const byte PWM_SEQUENCE_TIMING_REG = 0x24;
    public const byte PWM_ENERGY_MANAGER_REG = 0x25;
    public const byte RESERVED2 = 0x26;
    public const byte INDIVIDUAL_CH_SHUTDOWN_REG = 0x27;
    public const byte RESERVED3 = 0x28;

    public const byte INPUT_MUX_CH12_REG = 0x30;
    public const byte INPUT_MUX_CH34_REG = 0x31;
    public const byte INPUT_MUX_CH56_REG = 0x32;
    public const byte INPUT_MUX_CH78_REG = 0x33;

    public const byte PWM_MUX_CH12_REG = 0x34;
    public const byte PWM_MUX_CH34_REG = 0x35;
    public const byte PWM_MUX_CH56_REG = 0x36;
    public const byte PWM_MUX_CH78_REG = 0x37;

    public const byte DELAY_CH1_BD_MODE_REG = 0x38;
    public const byte DELAY_CH2_BD_MODE_REG = 0x39;
    public const byte DELAY_CH3_BD_MODE_REG = 0x3A;
    public const byte DELAY_CH4_BD_MODE_REG = 0x3B;
    public const byte DELAY_CH5_BD_MODE_REG = 0x3C;
    public const byte DELAY_CH6_BD_MODE_REG = 0x3D;
    public const byte DELAY_CH7_BD_MODE_REG = 0x3E;
    public const byte DELAY_CH8_BD_MODE_REG = 0x3F;

    public const byte BANK_SWITCHING_CMD_REG = 0x40;
    public const byte INPUT_MIXER_REG = 0x41;

    public const byte BASS_MIXER = 0x49;
    public const byte BIQUAD_FILTER_REG = 0x51;
    public const byte BASS_TREBLE_REG = 0x89;

    public const byte LOUDNESS_LOG2_GAIN_REG = 0x91;
    public const byte LOUDNESS_LOG2_OFFSET_REG = 0x92;
    public const byte LOUDNESS_GAIN_REG = 0x93;
    public const byte LOUDNESS_OFFSET_REG = 0x94;
    public const byte LOUDNESS_BIQUAD_REG = 0x95;

    public const byte DRC1_CONTROL_REG = 0x96;
    public const byte DRC2_CONTROL_REG = 0x97;

    public const byte DRC1_ENERGY_REG = 0x98;
    public const byte DRC1_THRESHOLD_REG = 0x99;
    public const byte DRC1_SLOPE_REG = 0x9A;
    public const byte DRC1_OFFSET_REG = 0x9B;
    public const byte DRC1_ATTACK_DECAY_REG = 0x9C;

    public const byte DRC2_ENERGY_REG = 0x9D;
    public const byte DRC2_THRESHOLD_REG = 0x9E;
    public const byte DRC2_SLOPE_REG = 0x9F;
    public const byte DRC2_OFFSET_REG = 0xA0;
    public const byte DRC2_ATTACK_DECAY_REG = 0xA1;

    public const byte DRC_BYPASS1_REG = 0xA2;
    public const byte DRC_BYPASS2_REG = 0xA3;
    public const byte DRC_BYPASS3_REG = 0xA4;
    public const byte DRC_BYPASS4_REG = 0xA5;
    public const byte DRC_BYPASS5_REG = 0xA6;
    public const byte DRC_BYPASS6_REG = 0xA7;
    public const byte DRC_BYPASS7_REG = 0xA8;
    public const byte DRC_BYPASS8_REG = 0xA9;

    public const byte OUTPUT_TO_PWM1_REG = 0xAA;
    public const byte OUTPUT_TO_PWM2_REG = 0xAB;
    public const byte OUTPUT_TO_PWM3_REG = 0xAC;
    public const byte OUTPUT_TO_PWM4_REG = 0xAD;
    public const byte OUTPUT_TO_PWM5_REG = 0xAE;
    public const byte OUTPUT_TO_PWM6_REG = 0xAF;
    public const byte OUTPUT_TO_PWM7_REG = 0xB0;
    public const byte OUTPUT_TO_PWM8_REG = 0xB1;

    public const byte ENERGY_MANAGER_AVERAGING_REG = 0xB2;
    public const byte ENERGY_MANAGER_WEIGHTING_CH1_REG = 0xB3;
    public const byte ENERGY_MANAGER_WEIGHTING_CH2_REG = 0xB4;
    public const byte ENERGY_MANAGER_WEIGHTING_CH3_REG = 0xB5;
    public const byte ENERGY_MANAGER_WEIGHTING_CH4_REG = 0xB6;
    public const byte ENERGY_MANAGER_WEIGHTING_CH5_REG = 0xB7;
    public const byte ENERGY_MANAGER_WEIGHTING_CH6_REG = 0xB8;
    public const byte ENERGY_MANAGER_WEIGHTING_CH7_REG = 0xB9;
    public const byte ENERGY_MANAGER_WEIGHTING_CH8_REG = 0xBA;

    public const byte ENERGY_MANAGER_HIGH_THRESHOLD_SATELLITE_REG = 0xBB;
    public const byte ENERGY_MANAGER_LOW_THRESHOLD_SATELLITE_REG = 0xBC;
    public const byte ENERGY_MANAGER_HIGH_THRESHOLD_SUBWOOFER_REG = 0xBD;
    public const byte ENERGY_MANAGER_LOW_THRESHOLD_SUBWOOFER_REG = 0xBE;
    public const byte RESERVED4 = 0xBF;

    public const byte ASRC_STATUS_REG = 0xC3;
    public const byte ASRC_CONTROL_REG = 0xC4;
    public const byte ASRC_MODE_CONTROL_REG = 0xC5;
    public const byte RESERVED5 = 0xC6;

    public const byte AUTO_MUTE_BEHAVIOUR = 0xCC;
    public const byte RESERVED6 = 0xCD;
    public const byte PSVC_VOLUME_BIQUAD = 0xCF;
    public const byte VOLUME_TREBLE_BASS_SLEW_RATES_REG = 0xD0;

    public const byte CH1_VOLUME_REG = 0xD1;
    public const byte CH2_VOLUME_REG = 0xD2;
    public const byte CH3_VOLUME_REG = 0xD3;
    public const byte CH4_VOLUME_REG = 0xD4;
    public const byte CH5_VOLUME_REG = 0xD5;
    public const byte CH6_VOLUME_REG = 0xD6;
    public const byte CH7_VOLUME_REG = 0xD7;
    public const byte CH8_VOLUME_REG = 0xD8;
    public const byte MASTER_VOLUME_REG = 0xD9;
    public const byte BASS_FILTER_SET_REG = 0xDA;
    public const byte BASS_FILTER_INDEX_REG = 0xDB;
    public const byte TREBLE_FILTER_SET_REG = 0xDC;
    public const byte TREBLE_FILTER_INDEX_REG = 0xDD;
    public const byte AM_MODE_REG = 0xDE;
    public const byte PSVC_RANGE_REG = 0xDF;
    public const byte GENERAL_CONTROL_REG = 0xE0;
    public const byte RESERVED7 = 0xE1;

    public const byte R_DOLBY_COEFLR_REG = 0xE3;
    public const byte R_DOLBY_COEFC_REG = 0xE4;
    public const byte R_DOLBY_COEFLSP_REG = 0xE5;
    public const byte R_DOLBY_COEFRSP_REG = 0xE6;
    public const byte R_DOLBY_COEFLSM_REG = 0xE7;
    public const byte R_DOLBY_COEFRSM_REG = 0xE8;

    public const byte THD_MANAGER_PRE_REG = 0xE9;
    public const byte THD_MANAGER_POST_REG = 0xEA;
    public const byte RESERVED8 = 0xEB;

    public const byte SDIN5_INPUT1_MIX_REG = 0xEC;
    public const byte SDIN5_INPUT2_MIX_REG = 0xED;
    public const byte SDIN5_INPUT3_MIX_REG = 0xEE;
    public const byte SDIN5_INPUT4_MIX_REG = 0xEF;
    public const byte SDIN5_INPUT5_MIX_REG = 0xF0;
    public const byte SDIN5_INPUT6_MIX_REG = 0xF1;
    public const byte SDIN5_INPUT7_MIX_REG = 0xF2;
    public const byte SDIN5_INPUT8_MIX_REG = 0xF3;

    public const byte KHZ192_PROCESS_FLOW_OUTPUT_MIX1_REG = 0xF4;
    public const byte KHZ192_PROCESS_FLOW_OUTPUT_MIX2_REG = 0xF5;
    public const byte KHZ192_PROCESS_FLOW_OUTPUT_MIX3_REG = 0xF6;
    public const byte KHZ192_PROCESS_FLOW_OUTPUT_MIX4_REG = 0xF7;
    public const byte RESERVED9 = 0xF8;

    public const byte KHZ192_IMAGE_SELECT_REG = 0xFA;
    public const byte KHZ192_DOLBY_DOWNMIX_COEF_REG = 0xFB;
    public const byte RESERVED10 = 0xFD;

    public const byte SPECIAL_REG = 0xFE;
    public const byte RESERVED11 = 0xFF;

    public const byte DATA_RATE_MASK = 0xE0;
    public const byte DATA_RATE_32KHZ = 0x00;
    public const byte DATA_RATE_44_1KHZ = 0x40;
    public const byte DATA_RATE_48KHZ = 0x60;
    public const byte DATA_RATE_88_2KHZ = 0x80;
    public const byte DATA_RATE_96KHZ = 0xA0;
    public const byte DATA_RATE_176_4KHZ = 0xC0;
    public const byte DATA_RATE_192KHZ = 0xE0;

    public const byte MCLK_FREQ_MASK = 0x1C;
    public const byte MCLK_FREQ_64 = 0x00;
    public const byte MCLK_FREQ_128 = 0x04;
    public const byte MCLK_FREQ_192 = 0x08;
    public const byte MCLK_FREQ_256 = 0x0C;
    public const byte MCLK_FREQ_384 = 0x10;
    public const byte MCLK_FREQ_512 = 0x14;
    public const byte MCLK_FREQ_768 = 0x18;

    public const byte CLK_REG_VALID_MASK = 0x03;
    public const byte CLK_REG_VALID = 0x01;
    public const byte CLK_REG_NOT_VALID = 0x00;

    public const byte FRAME_SLIP_MASK = 0x08;
    public const byte CLIP_INDICATOR_MASK = 0x04;
    public const byte FAULTZ_MASK = 0x02;
}
